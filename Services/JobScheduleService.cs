using KenketsuNote.Data;
using KenketsuNote.Jobs;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KenketsuNote.Services;

/// <summary>
/// job_schedule テーブル（管理画面で編集するジョブ設定）をQuartzスケジューラへ反映する処理。
/// 設定の正はDBで、起動時とスケジュール変更時にこのクラス経由でスケジューラへ適用する。
/// </summary>
public static class JobScheduleService
{
    /// <summary>cron式を解釈するタイムゾーン。実行環境のTZに依存しないようJSTで固定する</summary>
    public static readonly TimeZoneInfo JapanTimeZone = ResolveJapanTimeZone();

    /// <summary>管理画面に表示する1ジョブぶんの状態</summary>
    public sealed class JobStatus
    {
        public string    JobName        { get; init; } = string.Empty;
        public string    DisplayName    { get; init; } = string.Empty;
        public string    Description    { get; init; } = string.Empty;
        public string    CronExpression { get; init; } = string.Empty;
        public bool      IsEnabled      { get; init; }
        public bool      IsRunning      { get; init; }
        /// <summary>次回実行時刻（JST）。無効なジョブやcron式が不正な場合はnull</summary>
        public DateTime? NextRunJst     { get; init; }
        public DateTime  UpdatedAt      { get; init; }
    }

    /// <summary>定義済みジョブの設定行を返す。DBに行が無いジョブは既定のcron式で作成する</summary>
    public static async Task<List<JobSchedule>> LoadOrCreateAllAsync(KenketsuNoteContext db)
    {
        var rows = await db.JobSchedules.ToListAsync();

        var added = false;
        foreach (var def in JobRegistry.Jobs)
        {
            if (rows.Any(r => r.JobName == def.Name)) continue;
            var row = new JobSchedule { JobName = def.Name, CronExpression = def.DefaultCron };
            db.JobSchedules.Add(row);
            rows.Add(row);
            added = true;
        }
        if (added) await db.SaveChangesAsync();

        // 定義から消えたジョブの行は無視し、並びは JobRegistry の定義順に合わせる
        return rows
            .Where(r => JobRegistry.Find(r.JobName) is not null)
            .OrderBy(r => Array.FindIndex(JobRegistry.Jobs, j => j.Name == r.JobName))
            .ToList();
    }

    /// <summary>全ジョブの設定をスケジューラへ反映する（起動時用）</summary>
    public static async Task<List<JobSchedule>> SyncAllAsync(KenketsuNoteContext db, IScheduler scheduler)
    {
        var rows = await LoadOrCreateAllAsync(db);
        foreach (var row in rows)
            await ApplyAsync(scheduler, row);
        return rows;
    }

    /// <summary>
    /// 1ジョブぶんの設定をスケジューラへ反映する。
    /// 無効化はトリガーの削除で表現する。ジョブ本体は StoreDurably 登録なので残り、
    /// 手動実行（バッチ実行ボタン）や再有効化はいつでも可能。
    /// </summary>
    public static async Task ApplyAsync(IScheduler scheduler, JobSchedule row)
    {
        var triggerKey = new TriggerKey(JobRegistry.TriggerName(row.JobName));

        if (!row.IsEnabled)
        {
            await scheduler.UnscheduleJob(triggerKey);
            return;
        }

        // 無効化中に過ぎた実行時刻を再有効化のタイミングでまとめて実行しないよう DoNothing
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(new JobKey(row.JobName))
            .WithCronSchedule(row.CronExpression, x => x
                .InTimeZone(JapanTimeZone)
                .WithMisfireHandlingInstructionDoNothing())
            .Build();

        if (await scheduler.CheckExists(triggerKey))
            await scheduler.RescheduleJob(triggerKey, trigger);
        else
            await scheduler.ScheduleJob(trigger);
    }

    public static bool IsValidCron(string? cron)
        => !string.IsNullOrWhiteSpace(cron) && Quartz.CronExpression.IsValidExpression(cron);

    /// <summary>cron式から次回実行時刻（JST）を求める。不正な式や次回が無い場合はnull</summary>
    public static DateTime? NextRunJst(string? cron)
    {
        if (!IsValidCron(cron)) return null;
        var expression = new Quartz.CronExpression(cron!) { TimeZone = JapanTimeZone };
        var next = expression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
        return next is null ? null : TimeZoneInfo.ConvertTime(next.Value, JapanTimeZone).DateTime;
    }

    /// <summary>管理画面表示用に、DB設定＋スケジューラの実行状況をまとめて返す</summary>
    public static async Task<List<JobStatus>> GetStatusesAsync(KenketsuNoteContext db, IScheduler scheduler)
    {
        var rows = await LoadOrCreateAllAsync(db);
        var runningJobNames = (await scheduler.GetCurrentlyExecutingJobs())
            .Select(j => j.JobDetail.Key.Name)
            .ToHashSet();

        return rows.Select(row =>
        {
            var def = JobRegistry.Find(row.JobName)!;
            return new JobStatus
            {
                JobName        = row.JobName,
                DisplayName    = def.DisplayName,
                Description    = def.Description,
                CronExpression = row.CronExpression,
                IsEnabled      = row.IsEnabled,
                IsRunning      = runningJobNames.Contains(row.JobName),
                NextRunJst     = row.IsEnabled ? NextRunJst(row.CronExpression) : null,
                UpdatedAt      = row.UpdatedAt,
            };
        }).ToList();
    }

    private static TimeZoneInfo ResolveJapanTimeZone()
    {
        foreach (var id in new[] { "Asia/Tokyo", "Tokyo Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException)  { }
            catch (InvalidTimeZoneException)   { }
        }
        Console.WriteLine("[Quartz] JSTのタイムゾーン情報が見つからないため実行環境のローカル時刻でcronを解釈します");
        return TimeZoneInfo.Local;
    }
}
