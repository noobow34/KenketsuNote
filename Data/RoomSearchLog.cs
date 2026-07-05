using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KenketsuNote.Data;

[Table("room_search_log")]
public class RoomSearchLog
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("searched_at")]
    public DateTimeOffset SearchedAt { get; set; }

    [Column("center_block_id")]
    public int? CenterBlockId { get; set; }

    [Column("pref_id")]
    public int? PrefId { get; set; }

    [Column("room_name")]
    [StringLength(100)]
    public string? RoomName { get; set; }

    [Column("city")]
    [StringLength(50)]
    public string? City { get; set; }

    [Column("can_whole")]
    public bool? CanWhole { get; set; }

    [Column("can_plasma")]
    public bool? CanPlasma { get; set; }

    [Column("can_platelet")]
    public bool? CanPlatelet { get; set; }

    [Column("whole_only")]
    public bool WholeOnly { get; set; }

    [Column("plasma_only")]
    public bool PlasmaOnly { get; set; }

    [Column("open_dows")]
    [StringLength(50)]
    public string? OpenDows { get; set; }

    [Column("no_lunch_break_dows")]
    [StringLength(20)]
    public string? NoLunchBreakDows { get; set; }

    [Column("time_day_type")]
    public int? TimeDayType { get; set; }

    [Column("whole_open_by")]
    public TimeOnly? WholeOpenBy { get; set; }

    [Column("whole_close_after")]
    public TimeOnly? WholeCloseAfter { get; set; }

    [Column("comp_open_by")]
    public TimeOnly? CompOpenBy { get; set; }

    [Column("comp_close_after")]
    public TimeOnly? CompCloseAfter { get; set; }

    [Column("include_closed")]
    public bool IncludeClosed { get; set; }

    [Column("result_count")]
    public int? ResultCount { get; set; }

    [Column("ip_address")]
    [StringLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    [StringLength(500)]
    public string? UserAgent { get; set; }

    [Column("is_admin")]
    public bool IsAdmin { get; set; }
}
