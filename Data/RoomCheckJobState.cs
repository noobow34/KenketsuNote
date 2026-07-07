using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("room_check_job_state")]
public class RoomCheckJobState
{
    [Key]
    [Column("id")]
    public int Id { get; set; } = 1;

    // 次回チェック開始位置（room_idの昇順offset）
    [Column("next_offset")]
    public int NextOffset { get; set; } = 0;

    [Column("last_run_at")]
    public DateTimeOffset? LastRunAt { get; set; }

    // JST での実行時刻（デフォルト 6:30）
    [Column("scheduled_hour")]
    public int ScheduledHour { get; set; } = 6;

    [Column("scheduled_minute")]
    public int ScheduledMinute { get; set; } = 30;

    // アクセスログ・検索ログの保持日数（デフォルト 90日）
    [Column("log_retention_days")]
    public int LogRetentionDays { get; set; } = 90;
}
