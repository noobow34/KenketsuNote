namespace KenketsuNote.Jobs;

/// <summary>
/// 管理画面から有効/無効・スケジュールを操作できるQuartzジョブの定義一覧。
/// ジョブを追加したらここに1行足せば、Quartzへの登録も管理画面の表示も自動で追従する。
/// </summary>
public static class JobRegistry
{
    /// <param name="Name">QuartzのJobKey名・job_scheduleテーブルのキー</param>
    /// <param name="DisplayName">管理画面に表示する名前</param>
    /// <param name="Description">管理画面に表示する説明</param>
    /// <param name="DefaultCron">job_scheduleに行がない場合に登録するcron式（JST）</param>
    public sealed record JobDefinition(
        string Name,
        string DisplayName,
        string Description,
        string DefaultCron,
        Type   JobType);

    public static readonly JobDefinition[] Jobs =
    [
        new("RoomInfoCheckJob", "ルーム情報チェック",
            "献血ルームの公式ページとDB登録情報の差分をGeminiでチェックします（1回10ルーム）。",
            "0 30 6 * * ?", typeof(RoomInfoCheckJob)),
        new("LogCleanupJob", "ログ削除",
            "アクセスログ・検索ログのうち、保持期間より古いものを削除します。",
            "0 0 3 * * ?", typeof(LogCleanupJob)),
    ];

    public static JobDefinition? Find(string jobName)
        => Jobs.FirstOrDefault(j => j.Name == jobName);

    public static string TriggerName(string jobName) => $"{jobName}-trigger";
}
