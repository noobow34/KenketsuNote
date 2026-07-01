using KenketsuNote.Data;
using KenketsuNote.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KenketsuNote.Controllers;

[Route("rooms")]
public class RoomSearchController : Controller
{
    private readonly KenketsuNoteContext _db;

    public RoomSearchController(KenketsuNoteContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.CenterBlocks = MasterData.CenterBlocks;
        ViewBag.Prefectures  = MasterData.Prefectures;
        return View();
    }

    [HttpGet("search")]
    public IActionResult Search(
        int? centerBlockId = null,
        int? prefId = null,
        string? roomName = null,
        string? city = null,
        bool? canWhole = null,
        bool? canPlasma = null,
        bool? canPlatelet = null,
        bool wholeOnly = false,
        bool plasmaOnly = false,
        // openDows: 営業している曜日（カンマ区切り 0-6、"irregular"を含む可）
        string? openDows = null,
        // noLunchBreakDows: 昼中断なし区分（カンマ区切り "平日","土日祝"）
        string? noLunchBreakDows = null,
        // 受付時間フィルタの対象区分: null=両方, 0=平日, 1=土日祝
        int? timeDayType = null,
        // 全血受付時間フィルタ
        string? wholeOpenBy = null,
        string? wholeCloseAfter = null,
        // 成分受付時間フィルタ
        string? compOpenBy = null,
        string? compCloseAfter = null,
        bool includeClosed = false)
    {
        var dayNames = new[] { "日", "月", "火", "水", "木", "金", "土" };

        var query = _db.KenketsuRooms
            .Include(r => r.Pref)
            .Include(r => r.BusinessHours)
            .AsQueryable();

        if (!includeClosed)
            query = query.Where(r => !r.IsClosed);

        if (prefId.HasValue)
            query = query.Where(r => r.PrefId == prefId.Value);
        else if (centerBlockId.HasValue)
            query = query.Where(r => r.Pref!.CenterBlockId == centerBlockId.Value);

        if (!string.IsNullOrWhiteSpace(roomName))
            query = query.Where(r => r.RoomName.Contains(roomName));

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(r => r.City != null && r.City.Contains(city));

        if (wholeOnly)
        {
            query = query.Where(r => r.CanWhole == true
                                  && r.CanPlasma == false && r.CanPlatelet == false);
        }
        else if (plasmaOnly)
        {
            query = query.Where(r => r.CanPlasma == true
                                  && r.CanWhole == false && r.CanPlatelet == false);
        }
        else
        {
            if (canWhole == true)    query = query.Where(r => r.CanWhole == true);
            if (canPlasma == true)   query = query.Where(r => r.CanPlasma == true);
            if (canPlatelet == true) query = query.Where(r => r.CanPlatelet == true);
        }

        // 「〇曜日に営業」= closed_days にその曜日名が含まれない
        if (!string.IsNullOrWhiteSpace(openDows))
        {
            foreach (var token in openDows.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t == "irregular")
                    query = query.Where(r => r.ClosedDays != null && r.ClosedDays.Contains("不定休"));
                else if (int.TryParse(t, out var dow) && dow >= 0 && dow <= 6)
                {
                    var dn = dayNames[dow];
                    query = query.Where(r => r.ClosedDays == null || !r.ClosedDays.Contains(dn));
                }
            }
        }

        // 営業時間フィルタはToList後に評価（TimeOnly比較がEF変換できないケースを避けるため）
        var rooms = query
            .OrderBy(r => r.Pref!.DisplayOrder)
            .ThenBy(r => r.DisplayOrder)
            .ToList();

        // 「〇区分に昼中断なし」= 該当day_typeの全血・成分いずれも昼中断がない
        if (!string.IsNullOrWhiteSpace(noLunchBreakDows))
        {
            foreach (var token in noLunchBreakDows.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var dayType = token.Trim() == "平日" ? 0 : 1;
                rooms = rooms.Where(r =>
                    r.BusinessHours.Any(h => h.DayType == dayType
                        && h.WholeLunchStart == null && h.CompLunchStart == null)).ToList();
            }
        }

        // 受付時間フィルタ: timeDayType が指定されていればその区分のみ対象
        IEnumerable<RoomBusinessHours> HoursOf(KenketsuRoom r) =>
            timeDayType.HasValue ? r.BusinessHours.Where(h => h.DayType == timeDayType.Value) : r.BusinessHours;

        if (!string.IsNullOrWhiteSpace(wholeOpenBy) && TimeOnly.TryParse(wholeOpenBy, out var wholeOpenByTime))
            rooms = rooms.Where(r => HoursOf(r).Any(h => h.WholeReceptionStart.HasValue && h.WholeReceptionStart.Value <= wholeOpenByTime)).ToList();

        if (!string.IsNullOrWhiteSpace(wholeCloseAfter) && TimeOnly.TryParse(wholeCloseAfter, out var wholeCloseAfterTime))
            rooms = rooms.Where(r => HoursOf(r).Any(h => h.WholeReceptionEnd.HasValue && h.WholeReceptionEnd.Value >= wholeCloseAfterTime)).ToList();

        if (!string.IsNullOrWhiteSpace(compOpenBy) && TimeOnly.TryParse(compOpenBy, out var compOpenByTime))
            rooms = rooms.Where(r => HoursOf(r).Any(h => h.CompReceptionStart.HasValue && h.CompReceptionStart.Value <= compOpenByTime)).ToList();

        if (!string.IsNullOrWhiteSpace(compCloseAfter) && TimeOnly.TryParse(compCloseAfter, out var compCloseAfterTime))
            rooms = rooms.Where(r => HoursOf(r).Any(h => h.CompReceptionEnd.HasValue && h.CompReceptionEnd.Value >= compCloseAfterTime)).ToList();

        var result = rooms.Select(r => new
        {
            r.RoomId,
            r.RoomName,
            r.ImagePath,
            r.City,
            PrefName     = r.Pref!.PrefName,
            r.IsClosed,
            r.Remark,
            r.RoomUrl,
            r.CanWhole,
            r.CanPlasma,
            r.CanPlatelet,
            r.ClosedDays,
            BusinessHours = r.BusinessHours
                .OrderBy(h => h.DayType)
                .Select(h => new
                {
                    h.DayType,
                    WholeReceptionStart = h.WholeReceptionStart?.ToString("HH:mm"),
                    WholeReceptionEnd   = h.WholeReceptionEnd?.ToString("HH:mm"),
                    WholeLunchStart     = h.WholeLunchStart?.ToString("HH:mm"),
                    WholeLunchEnd       = h.WholeLunchEnd?.ToString("HH:mm"),
                    CompReceptionStart  = h.CompReceptionStart?.ToString("HH:mm"),
                    CompReceptionEnd    = h.CompReceptionEnd?.ToString("HH:mm"),
                    CompLunchStart      = h.CompLunchStart?.ToString("HH:mm"),
                    CompLunchEnd        = h.CompLunchEnd?.ToString("HH:mm"),
                })
                .ToList(),
        });

        return Json(result);
    }
}
