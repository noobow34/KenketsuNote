using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("pref")]
public partial class Pref
{
    [Key]
    [Column("pref_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PrefId { get; set; }

    [Column("pref_name")]
    [StringLength(30)]
    public string? PrefName { get; set; }

    [ForeignKey("CenterBlock")]
    [Column("center_block")]
    public int CenterBlockId { get; set; }
    public CenterBlock? CenterBlock { get; set; }

    [Column("display_order")]
    public int? DisplayOrder { get; set; }
}
