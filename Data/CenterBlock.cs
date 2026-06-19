using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("center_block")]
public partial class CenterBlock
{
    [Key]
    [Column("center_block_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CenterBlockId { get; set; }

    [Column("center_block_name")]
    [StringLength(30)]
    public string? CenterBlockName { get; set; }

    [Column("display_order")]
    public int? DisplayOrder { get; set; }
}
