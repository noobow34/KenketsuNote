using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

/// <summary>各ユーザー画面のアクセス時にモーダル表示するお知らせ</summary>
[Table("announcement")]
public class Announcement
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("title")]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    /// <summary>任意の本文。管理者のみが登録するためHTMLをそのまま出力する。</summary>
    [Column("body")]
    public string? Body { get; set; }

    /// <summary>公開中フラグ。公開中のうち最も新しいものを1件だけ表示する。</summary>
    [Column("is_published")]
    public bool IsPublished { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>お知らせに載せる更新履歴の選択（中間テーブル）</summary>
[Table("announcement_change_log")]
public class AnnouncementChangeLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("announcement_id")]
    public int AnnouncementId { get; set; }

    [Column("change_log_id")]
    public int ChangeLogId { get; set; }
}
