using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("kenketsu_record")]
public class KenketsuRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    [StringLength(10)]
    public string UserId { get; set; } = string.Empty;

    [Column("donation_date")]
    public DateOnly DonationDate { get; set; }

    /// <summary>種別: whole_200 / whole_400 / plasma / platelet</summary>
    [Column("donation_type")]
    [MaxLength(20)]
    public string DonationType { get; set; } = string.Empty;

    /// <summary>区分: plan（予定）/ actual（実績）</summary>
    [Column("record_type")]
    [MaxLength(10)]
    public string RecordType { get; set; } = string.Empty;

    /// <summary>全血のみ使用 (200 or 400)。成分は null。</summary>
    [Column("volume_ml")]
    public int? VolumeMl { get; set; }

    /// <summary>成分のみ使用 (plasma=1, platelet=2)。全血は null。</summary>
    [Column("component_count")]
    public int? ComponentCount { get; set; }

    [Column("room_id")]
    public int? RoomId { get; set; }

    [Column("notes")]
    [MaxLength(500)]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool IsWhole     => DonationType is "whole_200" or "whole_400";
    public bool IsComponent => DonationType is "plasma" or "platelet";
    public bool IsPlan      => RecordType == "plan";
    public bool IsActual    => RecordType == "actual";
}
