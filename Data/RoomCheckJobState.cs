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
}
