using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("room_business_hours")]
public class RoomBusinessHours
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("room_id")]
    public int RoomId { get; set; }

    /// <summary>区分: 0=平日, 1=土日祝</summary>
    [Column("day_type")]
    public int DayType { get; set; }

    [Column("whole_reception_start")]
    public TimeOnly? WholeReceptionStart { get; set; }

    [Column("whole_reception_end")]
    public TimeOnly? WholeReceptionEnd { get; set; }

    [Column("whole_lunch_start")]
    public TimeOnly? WholeLunchStart { get; set; }

    [Column("whole_lunch_end")]
    public TimeOnly? WholeLunchEnd { get; set; }

    [Column("comp_reception_start")]
    public TimeOnly? CompReceptionStart { get; set; }

    [Column("comp_reception_end")]
    public TimeOnly? CompReceptionEnd { get; set; }

    [Column("comp_lunch_start")]
    public TimeOnly? CompLunchStart { get; set; }

    [Column("comp_lunch_end")]
    public TimeOnly? CompLunchEnd { get; set; }

    [ForeignKey("RoomId")]
    public KenketsuRoom? Room { get; set; }
}
