using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("access_log")]
public class AccessLog
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("accessed_at")]
    public DateTimeOffset AccessedAt { get; set; }

    [Column("page")]
    [StringLength(200)]
    public string Page { get; set; } = "";

    [Column("is_admin")]
    public bool IsAdmin { get; set; }

    [Column("ip_address")]
    [StringLength(45)]
    public string? IpAddress { get; set; }
}
