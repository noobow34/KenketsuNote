using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

/// <summary>
/// Quartzジョブごとの実行スケジュール設定。
/// 管理画面から有効/無効とcron式を変更でき、起動時にこの内容でトリガーを登録する。
/// </summary>
[Table("job_schedule")]
public class JobSchedule
{
    /// <summary>ジョブ名（<see cref="KenketsuNote.Jobs.JobRegistry"/> の定義名・QuartzのJobKeyと同じ）</summary>
    [Key]
    [Column("job_name")]
    [MaxLength(100)]
    public string JobName { get; set; } = string.Empty;

    /// <summary>Quartz形式のcron式（秒 分 時 日 月 曜日）。JSTで解釈する</summary>
    [Column("cron_expression")]
    [MaxLength(100)]
    public string CronExpression { get; set; } = string.Empty;

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
