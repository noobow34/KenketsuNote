using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace KenketsuNote.Data;

[Table("visit_stamp")]
public partial class VisitStamp
{
    [Key]
    [Column("stamp_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int StampId { get; set; }

    [Column("user_id")]
    public required string UserId { get; set; }

    [Column("room_id")]
    public required int RoomId { get; set; }

    [Column("visit_date")]
    public DateOnly? VisitDate { get; set; }

    [Column("angle")]
    [Precision(5, 1)]
    public double Angle { get; set; }

    [Column("created_at")]
    [Precision(6, 0)]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    [Precision(6, 0)]
    public DateTime? UpdatedAt { get; set; }
}
