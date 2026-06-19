using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("center_block_order")]
public partial class CenterBlockOrder
{
    [Key]
    [Column("order_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int OrderId { get; set; }

    [Column("center_block_id")]
    public int CenterBlockId { get; set; }

    [Column("user_id")]
    [StringLength(10)]
    public required string UserId { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }
}
