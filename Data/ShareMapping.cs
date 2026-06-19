using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("share_mapping", Schema = "ashiato")]
public class ShareMapping
{
    [Key]
    [Column("share_id")]
    public required string ShareId { get; set; }

    [Column("original_id")]
    public required string OriginalId { get; set; }
}
