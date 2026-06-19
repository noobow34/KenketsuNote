using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("pref_order", Schema = "ashiato")]
public partial class PrefOrder
{
    [Key]
    [Column("order_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int OrderId { get; set; }

    [Column("pref_id")]
    public int PrefId { get; set; }

    [Column("user_id")]
    [StringLength(10)]
    public required string UserId { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }
}
