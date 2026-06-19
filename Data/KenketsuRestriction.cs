using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("kenketsu_restriction", Schema = "ashiato")]
public class KenketsuRestriction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    [StringLength(10)]
    public string UserId { get; set; } = string.Empty;

    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("duration_days")]
    public int DurationDays { get; set; }

    [NotMapped]
    public DateOnly EndDate => StartDate.AddDays(DurationDays - 1);

    [Column("reason")]
    [MaxLength(500)]
    public string? Reason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
}
