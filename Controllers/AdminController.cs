using System.Text.Json;
using KenketsuNote.Auth;
using KenketsuNote.Data;
using KenketsuNote.Infrastructure;
using KenketsuNote.Jobs;
using KenketsuNote.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KenketsuNote.Controllers;

[Route("admin")]
public class AdminController : Controller
{
    private readonly KenketsuNoteContext _db;
    private readonly ISchedulerFactory _schedulerFactory;

    public AdminController(KenketsuNoteContext db, ISchedulerFactory schedulerFactory)
    {
        _db = db;
        _schedulerFactory = schedulerFactory;
    }

    private const int CheckPageSize = 20;
    private const int LogPageSize   = 50;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] int checkPage = 1, [FromQuery] int logPage = 1, [FromQuery] bool hideAdmin = true)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        checkPage = Math.Max(1, checkPage);
        logPage   = Math.Max(1, logPage);

        var checkTotal = await _db.RoomCheckResults.CountAsync();
        var checkResults = await _db.RoomCheckResults
            .Include(r => r.Room)
            .OrderBy(r => r.Resolved)
            .ThenByDescending(r => r.CheckedAt)
            .Skip((checkPage - 1) * CheckPageSize)
            .Take(CheckPageSize)
            .ToListAsync();

        var logQuery = _db.RoomSearchLogs.AsQueryable();
        if (hideAdmin) logQuery = logQuery.Where(l => !l.IsAdmin);
        var logTotal = await logQuery.CountAsync();
        var searchLogs = await logQuery
            .OrderByDescending(l => l.SearchedAt)
            .Skip((logPage - 1) * LogPageSize)
            .Take(LogPageSize)
            .ToListAsync();

        ViewBag.HideAdmin = hideAdmin;

        ViewBag.CheckResults  = checkResults;
        ViewBag.CheckPage     = checkPage;
        ViewBag.CheckTotalPages = (int)Math.Ceiling(checkTotal / (double)CheckPageSize);

        ViewBag.SearchLogs  = searchLogs;
        ViewBag.LogPage     = logPage;
        ViewBag.LogTotalPages = (int)Math.Ceiling(logTotal / (double)LogPageSize);

        var jsonOpt = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        ViewBag.RoomsJson  = JsonSerializer.Serialize(MasterData.Rooms.Where(r => !r.IsClosed).Select(r => new { r.RoomId, r.RoomName, r.PrefId, r.IsClosed }), jsonOpt);
        ViewBag.PrefsJson  = JsonSerializer.Serialize(MasterData.Prefectures.Select(p => new { p.PrefId, p.PrefName, p.CenterBlockId }), jsonOpt);
        ViewBag.BlocksJson = JsonSerializer.Serialize(MasterData.CenterBlocks.Select(b => new { b.CenterBlockId, b.CenterBlockName }), jsonOpt);

        ViewBag.ChangeLogs = await LoadChangeLogsAsync();
        await SetAnnouncementViewDataAsync();

        var jobState = await _db.RoomCheckJobStates.FindAsync(1);
        ViewBag.LogRetentionDays = jobState?.LogRetentionDays ?? 90;
        ViewBag.GeminiModel      = jobState?.GeminiModel      ?? RoomInfoCheckJob.DefaultGeminiModel;

        await SetJobViewDataAsync();

        // アクセス統計（直近14日）
        var since = DateTimeOffset.UtcNow.AddHours(9).AddDays(-13).Date;
        var accessQuery = _db.AccessLogs.Where(l => l.AccessedAt >= since);
        if (hideAdmin) accessQuery = accessQuery.Where(l => !l.IsAdmin);
        var accessStats = await accessQuery
            .GroupBy(l => new { l.Page, Date = l.AccessedAt.AddHours(9).Date })
            .Select(g => new { g.Key.Page, g.Key.Date, Count = g.Count() })
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.Page)
            .ToListAsync();
        ViewBag.AccessStats = accessStats;

        return View();
    }

    [HttpPost("run-room-check")]
    public async Task<IActionResult> RunRoomCheck()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey("RoomInfoCheckJob");
            await scheduler.TriggerJob(jobKey);
            return Json(new { success = true, message = "ジョブを起動しました。結果はしばらく後にログに反映されます。" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"エラー: {ex.Message}" });
        }
    }

    [HttpPost("run-room-check-single")]
    public async Task<IActionResult> RunRoomCheckSingle([FromForm] int roomId)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        try
        {
            var room = await _db.KenketsuRooms
                .Include(r => r.BusinessHours)
                .FirstOrDefaultAsync(r => r.RoomId == roomId);
            if (room is null) return Json(new { success = false, message = "ルームが見つかりません。" });
            if (room.RoomUrl is null) return Json(new { success = false, message = "このルームには公式URLが登録されていません。" });

            var geminiApiKey  = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
            var slackBotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
            var slackChannel  = Environment.GetEnvironmentVariable("SLACK_ROOM_CHECK_CHANNEL") ?? "";
            var baseUrl       = (Environment.GetEnvironmentVariable("KENKETSUNOTE_BASE_URL") ?? "").TrimEnd('/');

            if (string.IsNullOrEmpty(geminiApiKey))
                return Json(new { success = false, message = "GEMINI_API_KEY が設定されていません。" });

            var jobState    = await _db.RoomCheckJobStates.FindAsync(1);
            var geminiModel = string.IsNullOrWhiteSpace(jobState?.GeminiModel) ? RoomInfoCheckJob.DefaultGeminiModel : jobState.GeminiModel;

            await RoomInfoCheckJob.ProcessRoomAsync(room, _db, geminiApiKey, geminiModel, slackBotToken, slackChannel, baseUrl);
            return Json(new { success = true, message = $"「{room.RoomName}」のチェックが完了しました。ログを確認してください。" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"エラー: {ex.Message}" });
        }
    }

    [HttpGet("check-logs")]
    public async Task<IActionResult> CheckLogs([FromQuery] int checkPage = 1)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        checkPage = Math.Max(1, checkPage);
        var checkTotal = await _db.RoomCheckResults.CountAsync();
        var checkResults = await _db.RoomCheckResults
            .Include(r => r.Room)
            .OrderBy(r => r.Resolved)
            .ThenByDescending(r => r.CheckedAt)
            .Skip((checkPage - 1) * CheckPageSize)
            .Take(CheckPageSize)
            .ToListAsync();
        ViewBag.CheckResults    = checkResults;
        ViewBag.CheckPage       = checkPage;
        ViewBag.CheckTotalPages = (int)Math.Ceiling(checkTotal / (double)CheckPageSize);
        return PartialView("_CheckLogTable");
    }

    [HttpGet("search-logs")]
    public async Task<IActionResult> SearchLogs([FromQuery] int logPage = 1, [FromQuery] bool hideAdmin = true)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        logPage = Math.Max(1, logPage);
        var logQuery = _db.RoomSearchLogs.AsQueryable();
        if (hideAdmin) logQuery = logQuery.Where(l => !l.IsAdmin);
        var logTotal = await logQuery.CountAsync();
        var searchLogs = await logQuery
            .OrderByDescending(l => l.SearchedAt)
            .Skip((logPage - 1) * LogPageSize)
            .Take(LogPageSize)
            .ToListAsync();
        ViewBag.SearchLogs    = searchLogs;
        ViewBag.LogPage       = logPage;
        ViewBag.LogTotalPages = (int)Math.Ceiling(logTotal / (double)LogPageSize);
        ViewBag.HideAdmin     = hideAdmin;
        return PartialView("_SearchLogTable");
    }

    // ─────────────────────────────────────────────
    // ジョブ管理（Quartz）
    // ─────────────────────────────────────────────
    [HttpGet("jobs")]
    public async Task<IActionResult> Jobs()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        await SetJobViewDataAsync();
        return PartialView("_JobTable");
    }

    [HttpPost("job/update-schedule")]
    public async Task<IActionResult> UpdateJobSchedule([FromForm] string jobName, [FromForm] string cron)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        var def = JobRegistry.Find(jobName ?? "");
        if (def is null) return Json(new { success = false, message = "ジョブが見つかりません。" });

        cron = (cron ?? "").Trim();
        if (!JobScheduleService.IsValidCron(cron))
            return Json(new { success = false, message = "cron式が不正です。（例: 毎日6:30 → 0 30 6 * * ?）" });
        if (cron.Length > 100)
            return Json(new { success = false, message = "cron式は100文字以内で指定してください。" });

        var row = await FindOrCreateJobScheduleAsync(def);
        row.CronExpression = cron;
        row.UpdatedAt      = DateTime.Now;
        await _db.SaveChangesAsync();

        var scheduler = await _schedulerFactory.GetScheduler();
        await JobScheduleService.ApplyAsync(scheduler, row);

        var next = JobScheduleService.NextRunJst(cron);
        var suffix = row.IsEnabled
            ? $"次回実行は {next:yyyy-MM-dd HH:mm} (JST) です。"
            : "このジョブは無効化中のため、有効化するまで実行されません。";
        return Json(new { success = true, message = $"「{def.DisplayName}」のスケジュールを変更しました。{suffix}" });
    }

    [HttpPost("job/toggle-enabled")]
    public async Task<IActionResult> ToggleJobEnabled([FromForm] string jobName)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        var def = JobRegistry.Find(jobName ?? "");
        if (def is null) return Json(new { success = false, message = "ジョブが見つかりません。" });

        var row = await FindOrCreateJobScheduleAsync(def);
        row.IsEnabled = !row.IsEnabled;
        row.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        var scheduler = await _schedulerFactory.GetScheduler();
        await JobScheduleService.ApplyAsync(scheduler, row);

        var message = row.IsEnabled
            ? $"「{def.DisplayName}」を有効化しました。次回実行は {JobScheduleService.NextRunJst(row.CronExpression):yyyy-MM-dd HH:mm} (JST) です。"
            : $"「{def.DisplayName}」を無効化しました。自動実行されなくなります。";
        return Json(new { success = true, message });
    }

    private async Task<Data.JobSchedule> FindOrCreateJobScheduleAsync(JobRegistry.JobDefinition def)
    {
        var row = await _db.JobSchedules.FindAsync(def.Name);
        if (row is null)
        {
            row = new Data.JobSchedule { JobName = def.Name, CronExpression = def.DefaultCron };
            _db.JobSchedules.Add(row);
        }
        return row;
    }

    private async Task SetJobViewDataAsync()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        ViewBag.Jobs = await JobScheduleService.GetStatusesAsync(_db, scheduler);
    }

    [HttpPost("update-log-retention")]
    public async Task<IActionResult> UpdateLogRetention([FromForm] int days)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        if (days < 1 || days > 3650)
            return Json(new { success = false, message = "保持日数は1〜3650日の範囲で指定してください。" });

        var state = await _db.RoomCheckJobStates.FindAsync(1);
        if (state is null)
        {
            state = new RoomCheckJobState { Id = 1, NextOffset = 0 };
            _db.RoomCheckJobStates.Add(state);
        }
        state.LogRetentionDays = days;
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = $"ログ保持期間を {days} 日に変更しました。" });
    }

    [HttpGet("gemini-models")]
    public async Task<IActionResult> GeminiModels()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
            return Json(new { success = false, message = "GEMINI_API_KEY が設定されていません。" });

        try
        {
            var models = await RoomInfoCheckJob.ListAvailableModelsAsync(apiKey);
            return Json(new { success = true, models });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"エラー: {ex.Message}" });
        }
    }

    [HttpPost("update-gemini-model")]
    public async Task<IActionResult> UpdateGeminiModel([FromForm] string model)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        model = (model ?? "").Trim();
        if (string.IsNullOrEmpty(model))
            return Json(new { success = false, message = "モデル名を入力してください。" });

        var state = await _db.RoomCheckJobStates.FindAsync(1);
        if (state is null)
        {
            state = new RoomCheckJobState { Id = 1, NextOffset = 0 };
            _db.RoomCheckJobStates.Add(state);
        }
        state.GeminiModel = model;
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = $"Geminiモデルを「{model}」に変更しました。" });
    }

    // ─────────────────────────────────────────────
    // 更新履歴（トップページ表示）
    // ─────────────────────────────────────────────
    [HttpGet("change-logs")]
    public async Task<IActionResult> ChangeLogs()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        ViewBag.ChangeLogs = await LoadChangeLogsAsync();
        await SetAnnouncementViewDataAsync();
        return PartialView("_ChangeLogTable");
    }

    [HttpPost("change-log/save")]
    public async Task<IActionResult> SaveChangeLog([FromForm] int? id, [FromForm] string releasedOn, [FromForm] string content)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        if (!DateOnly.TryParse(releasedOn, out var date))
            return Json(new { success = false, message = "日付が不正です。" });

        content = (content ?? "").Trim();
        if (string.IsNullOrEmpty(content))
            return Json(new { success = false, message = "内容を入力してください。" });
        if (content.Length > 500)
            return Json(new { success = false, message = "内容は500文字以内で入力してください。" });

        if (id is > 0)
        {
            var entry = await _db.ChangeLogs.FindAsync(id.Value);
            if (entry is null) return Json(new { success = false, message = "対象が見つかりません。" });
            entry.ReleasedOn = date;
            entry.Content    = content;
            entry.UpdatedAt  = DateTime.Now;
        }
        else
        {
            _db.ChangeLogs.Add(new ChangeLog { ReleasedOn = date, Content = content });
        }
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = id is > 0 ? "更新しました。" : "追加しました。" });
    }

    [HttpPost("change-log/delete")]
    public async Task<IActionResult> DeleteChangeLog([FromForm] int id)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        var entry = await _db.ChangeLogs.FindAsync(id);
        if (entry is null) return Json(new { success = false, message = "対象が見つかりません。" });

        _db.ChangeLogs.Remove(entry);
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "削除しました。" });
    }

    // ─────────────────────────────────────────────
    // お知らせ（ユーザー画面のモーダル表示）
    // ─────────────────────────────────────────────
    [HttpGet("announcements")]
    public async Task<IActionResult> Announcements()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        await SetAnnouncementViewDataAsync();
        return PartialView("_AnnouncementTable");
    }

    [HttpPost("announcement/save")]
    public async Task<IActionResult> SaveAnnouncement(
        [FromForm] int? id, [FromForm] string title, [FromForm] string? body,
        [FromForm] bool isPublished, [FromForm] string? changeLogIds)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        title = (title ?? "").Trim();
        if (string.IsNullOrEmpty(title))
            return Json(new { success = false, message = "タイトルを入力してください。" });
        if (title.Length > 100)
            return Json(new { success = false, message = "タイトルは100文字以内で入力してください。" });

        body = string.IsNullOrWhiteSpace(body) ? null : body.Trim();

        var selectedIds = (changeLogIds ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .ToList();

        if (body == null && selectedIds.Count == 0)
            return Json(new { success = false, message = "本文か更新履歴のどちらかは指定してください。" });

        var validIds = await _db.ChangeLogs.Where(c => selectedIds.Contains(c.Id)).Select(c => c.Id).ToListAsync();

        Announcement entry;
        if (id is > 0)
        {
            var existing = await _db.Announcements.FindAsync(id.Value);
            if (existing is null) return Json(new { success = false, message = "対象が見つかりません。" });

            entry             = existing;
            entry.Title       = title;
            entry.Body        = body;
            entry.IsPublished = isPublished;
            entry.UpdatedAt   = DateTime.Now;

            var links = _db.AnnouncementChangeLogs.Where(l => l.AnnouncementId == entry.Id);
            _db.AnnouncementChangeLogs.RemoveRange(links);
        }
        else
        {
            entry = new Announcement { Title = title, Body = body, IsPublished = isPublished };
            _db.Announcements.Add(entry);
        }
        await _db.SaveChangesAsync();

        foreach (var logId in validIds)
            _db.AnnouncementChangeLogs.Add(new AnnouncementChangeLog { AnnouncementId = entry.Id, ChangeLogId = logId });
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = id is > 0 ? "更新しました。" : "追加しました。" });
    }

    [HttpPost("announcement/toggle-publish")]
    public async Task<IActionResult> ToggleAnnouncementPublish([FromForm] int id)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        var entry = await _db.Announcements.FindAsync(id);
        if (entry is null) return Json(new { success = false, message = "対象が見つかりません。" });

        entry.IsPublished = !entry.IsPublished;
        entry.UpdatedAt   = DateTime.Now;
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = entry.IsPublished ? "公開しました。" : "公開を停止しました。" });
    }

    [HttpPost("announcement/delete")]
    public async Task<IActionResult> DeleteAnnouncement([FromForm] int id)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        var entry = await _db.Announcements.FindAsync(id);
        if (entry is null) return Json(new { success = false, message = "対象が見つかりません。" });

        _db.AnnouncementChangeLogs.RemoveRange(_db.AnnouncementChangeLogs.Where(l => l.AnnouncementId == id));
        _db.Announcements.Remove(entry);
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "削除しました。" });
    }

    private async Task SetAnnouncementViewDataAsync()
    {
        var announcements = await _db.Announcements
            .AsNoTracking()
            .OrderByDescending(a => a.Id)
            .ToListAsync();

        var links = await _db.AnnouncementChangeLogs.AsNoTracking().ToListAsync();

        ViewBag.Announcements = announcements;
        ViewBag.AnnouncementLogIds = announcements.ToDictionary(
            a => a.Id,
            a => links.Where(l => l.AnnouncementId == a.Id).Select(l => l.ChangeLogId).ToList());
    }

    private Task<List<ChangeLog>> LoadChangeLogsAsync()
        => _db.ChangeLogs
            .AsNoTracking()
            .OrderByDescending(c => c.ReleasedOn)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

    [HttpPost("reload-master")]
    public IActionResult ReloadMaster()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        try
        {
            MasterData.Load();
            return Json(new
            {
                success = true,
                message = $"マスタデータを再ロードしました（ルーム {MasterData.Rooms.Length} 件 / 都道府県 {MasterData.Prefectures.Length} 件 / ブロック {MasterData.CenterBlocks.Length} 件）"
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"エラー: {ex.Message}" });
        }
    }
}
