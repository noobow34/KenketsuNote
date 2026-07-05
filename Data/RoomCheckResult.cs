using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("room_check_result")]
public class RoomCheckResult
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [ForeignKey("Room")]
    [Column("room_id")]
    public int RoomId { get; set; }

    public KenketsuRoom? Room { get; set; }

    [Column("checked_at")]
    public DateTimeOffset CheckedAt { get; set; }

    // Geminiが抽出した現在値（JSON文字列）
    [Column("gemini_result")]
    public string? GeminiResult { get; set; }

    // 差分の説明（カンマ区切り）
    [Column("changes")]
    [StringLength(1000)]
    public string? Changes { get; set; }

    [Column("has_changes")]
    public bool HasChanges { get; set; }

    // 対応済みフラグ（管理画面から更新）
    [Column("resolved")]
    public bool Resolved { get; set; } = false;

    // レビューURL用トークン（推測不可能なGUID）
    [Column("review_token")]
    public Guid ReviewToken { get; set; } = Guid.NewGuid();
}
