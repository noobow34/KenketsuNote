using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("kenketsu_room")]
public partial class KenketsuRoom
{
    [Key]
    [Column("room_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RoomId { get; set; }

    [ForeignKey("Pref")]
    [Column("pref")]
    public int PrefId { get; set; }
    public Pref? Pref { get; set; }

    [Column("room_name")]
    [StringLength(100)]
    public string RoomName { get; set; } = null!;

    [Column("display_order")]
    public int? DisplayOrder { get; set; }

    [Column("image_path")]
    [StringLength(100)]
    public string? ImagePath { get; set; }

    [Column("is_closed")]
    public bool IsClosed { get; set; } = false;

    [Column("remark")]
    [StringLength(200)]
    public string? Remark { get; set; }

    [Column("city")]
    [StringLength(50)]
    public string? City { get; set; }

    [Column("can_whole_200")]
    public bool? CanWhole200 { get; set; }

    [Column("can_whole_400")]
    public bool? CanWhole400 { get; set; }

    [Column("can_plasma")]
    public bool? CanPlasma { get; set; }

    [Column("can_platelet")]
    public bool? CanPlatelet { get; set; }

    [Column("closed_days")]
    [StringLength(200)]
    public string? ClosedDays { get; set; }

    [Column("room_url")]
    [StringLength(500)]
    public string? RoomUrl { get; set; }

    public ICollection<RoomBusinessHours> BusinessHours { get; set; } = [];
}
