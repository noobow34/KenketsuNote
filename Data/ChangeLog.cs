using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

/// <summary>トップページに表示する更新履歴の1件</summary>
[Table("change_log")]
public class ChangeLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>公開日（トップページに表示する日付）</summary>
    [Column("released_on")]
    public DateOnly ReleasedOn { get; set; }

    [Column("content")]
    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
