using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("users")]
public partial class User
{
    [Key]
    [Column("user_id")]
    [StringLength(10)]
    public string UserId { get; set; } = null!;

    [Column("registered_at")]
    public DateTime? RegisteredAt { get; set; }

    [Column("last_access_at")]
    public DateTime? LastAccessAt { get; set; }

    [Column("user_name")]
    [StringLength(50)]
    public string? UserName { get; set; }

    [Column("show_closed_default")]
    public bool ShowClosedDefault { get; set; } = false;

    /// <summary>性別: "male" / "female" / null（未設定）</summary>
    [Column("gender")]
    [StringLength(6)]
    public string? Gender { get; set; }

    [Column("migrated_from_ashiato")]
    public bool MigratedFromAshiato { get; set; } = false;

    [Column("share_show_history")]
    public bool ShareShowHistory { get; set; } = false;

    /// <summary>「次回から表示しない」を押したお知らせのID。より新しいお知らせが公開されれば再度表示される。</summary>
    [Column("dismissed_announcement_id")]
    public int? DismissedAnnouncementId { get; set; }
}
