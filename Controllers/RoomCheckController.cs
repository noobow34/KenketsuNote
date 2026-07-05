using System.Text.Json;
using KenketsuNote.Data;
using KenketsuNote.Jobs;
using KenketsuNote.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KenketsuNote.Controllers;

[Route("admin/room-check")]
public class RoomCheckController : Controller
{
    private readonly KenketsuNoteContext _db;

    public RoomCheckController(KenketsuNoteContext db) => _db = db;

    // レビュー画面
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Review(long id, [FromQuery] Guid token)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        var result = await _db.RoomCheckResults
            .Include(r => r.Room)
            .ThenInclude(r => r!.BusinessHours)
            .FirstOrDefaultAsync(r => r.Id == id && r.ReviewToken == token);

        if (result is null) return NotFound("リンクが無効です。");
        if (result.Resolved) return Content("この変更はすでに対応済みです。");

        RoomInfoCheckJob.GeminiRoomCheckResponse? gemini = null;
        if (result.GeminiResult is not null)
            gemini = JsonSerializer.Deserialize<RoomInfoCheckJob.GeminiRoomCheckResponse>(result.GeminiResult,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        ViewBag.Result = result;
        ViewBag.Gemini = gemini;
        ViewBag.Token  = token;
        return View();
    }

    // 承認：Gemini抽出値をkenketsu_roomに反映
    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromQuery] Guid token)
    {
        var result = await _db.RoomCheckResults
            .Include(r => r.Room)
            .ThenInclude(r => r!.BusinessHours)
            .FirstOrDefaultAsync(r => r.Id == id && r.ReviewToken == token);

        if (result is null) return NotFound("リンクが無効です。");
        if (result.Resolved) return Content("この変更はすでに対応済みです。");

        if (result.GeminiResult is not null && result.Room is not null)
        {
            var gemini = JsonSerializer.Deserialize<RoomInfoCheckJob.GeminiRoomCheckResponse>(result.GeminiResult,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (gemini is not null)
            {
                if (gemini.City        is not null) result.Room.City        = gemini.City;
                if (gemini.CanWhole    is not null) result.Room.CanWhole    = gemini.CanWhole;
                if (gemini.CanPlasma   is not null) result.Room.CanPlasma   = gemini.CanPlasma;
                if (gemini.CanPlatelet is not null) result.Room.CanPlatelet = gemini.CanPlatelet;
                if (gemini.ClosedDays  is not null) result.Room.ClosedDays  = gemini.ClosedDays;

                // 営業時間：既存を全削除して再挿入
                if (gemini.BusinessHours is { Count: > 0 })
                {
                    _db.RoomBusinessHours.RemoveRange(result.Room.BusinessHours);
                    foreach (var h in gemini.BusinessHours)
                    {
                        _db.RoomBusinessHours.Add(new RoomBusinessHours
                        {
                            RoomId               = result.Room.RoomId,
                            DayType              = h.DayType,
                            WholeReceptionStart  = ParseTime(h.WholeReceptionStart),
                            WholeReceptionEnd    = ParseTime(h.WholeReceptionEnd),
                            WholeLunchStart      = ParseTime(h.WholeLunchStart),
                            WholeLunchEnd        = ParseTime(h.WholeLunchEnd),
                            CompReceptionStart   = ParseTime(h.CompReceptionStart),
                            CompReceptionEnd     = ParseTime(h.CompReceptionEnd),
                            CompLunchStart       = ParseTime(h.CompLunchStart),
                            CompLunchEnd         = ParseTime(h.CompLunchEnd),
                        });
                    }
                }
            }
        }

        result.Resolved = true;
        await _db.SaveChangesAsync();

        return View("Approved");
    }

    private static TimeOnly? ParseTime(string? s) =>
        TimeOnly.TryParse(s, out var t) ? t : null;

    // 却下：変更を適用せず対応済みにする
    [HttpPost("{id:long}/dismiss")]
    public async Task<IActionResult> Dismiss(long id, [FromQuery] Guid token)
    {
        var result = await _db.RoomCheckResults
            .FirstOrDefaultAsync(r => r.Id == id && r.ReviewToken == token);

        if (result is null) return NotFound("リンクが無効です。");

        result.Resolved = true;
        await _db.SaveChangesAsync();

        return View("Dismissed");
    }
}
