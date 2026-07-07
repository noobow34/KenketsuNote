using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("room_dismissed_diff")]
public class RoomDismissedDiff
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("room_id")]
    public int RoomId { get; set; }

    // 'city' / 'can_whole' / 'can_plasma' / 'can_platelet' / 'closed_days' / 'business_hours_0' / 'business_hours_1'
    [Column("field")]
    [StringLength(50)]
    public string Field { get; set; } = "";

    // Geminiがページから抽出した値（スカラーは文字列、業務時間はJSON）
    [Column("gemini_value")]
    public string GeminiValue { get; set; } = "";

    [Column("dismissed_at")]
    public DateTimeOffset DismissedAt { get; set; }
}
