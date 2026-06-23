using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("kenketsu_restriction_preset")]
public class KenketsuRestrictionPreset
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("label")]
    [StringLength(100)]
    public string Label { get; set; } = string.Empty;

    [Column("duration_days")]
    public int DurationDays { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }
}
