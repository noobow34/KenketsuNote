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

    // 承認：選択された項目のみkenketsu_roomに反映（編集済み値を使用）
    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromQuery] Guid token, [FromForm] ApproveFormData form)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        var result = await _db.RoomCheckResults
            .Include(r => r.Room)
            .ThenInclude(r => r!.BusinessHours)
            .FirstOrDefaultAsync(r => r.Id == id && r.ReviewToken == token);

        if (result is null) return NotFound("リンクが無効です。");
        if (result.Resolved) return Content("この変更はすでに対応済みです。");

        // フォームの編集済み値を有効値として構築
        var effective = BuildEffective(form);

        if (result.Room is not null)
        {
            if (form.Fields.Contains("city")         && effective.City        is not null) result.Room.City        = effective.City;
            if (form.Fields.Contains("can_whole")    && effective.CanWhole    is not null) result.Room.CanWhole    = effective.CanWhole;
            if (form.Fields.Contains("can_plasma")   && effective.CanPlasma   is not null) result.Room.CanPlasma   = effective.CanPlasma;
            if (form.Fields.Contains("can_platelet") && effective.CanPlatelet is not null) result.Room.CanPlatelet = effective.CanPlatelet;
            if (form.Fields.Contains("closed_days")  && effective.ClosedDays  is not null) result.Room.ClosedDays  = effective.ClosedDays;

            var targetDayTypes = new List<int>();
            if (form.Fields.Contains("business_hours_0")) targetDayTypes.Add(0);
            if (form.Fields.Contains("business_hours_1")) targetDayTypes.Add(1);

            if (targetDayTypes.Count > 0 && effective.BusinessHours is { Count: > 0 })
            {
                var toRemove = result.Room.BusinessHours.Where(h => targetDayTypes.Contains(h.DayType)).ToList();
                _db.RoomBusinessHours.RemoveRange(toRemove);
                foreach (var h in effective.BusinessHours.Where(h => targetDayTypes.Contains(h.DayType)))
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

            await UpdateDismissedDiffsAsync(result.Room.RoomId, effective, approved: form.Fields);
        }

        result.Resolved = true;
        await _db.SaveChangesAsync();

        return View("Approved");
    }

    // 却下：変更を適用せず対応済みにする（全フィールドを却下済み差分として記録）
    [HttpPost("{id:long}/dismiss")]
    public async Task<IActionResult> Dismiss(long id, [FromQuery] Guid token)
    {
        var result = await _db.RoomCheckResults
            .FirstOrDefaultAsync(r => r.Id == id && r.ReviewToken == token);

        if (result is null) return NotFound("リンクが無効です。");

        RoomInfoCheckJob.GeminiRoomCheckResponse? gemini = null;
        if (result.GeminiResult is not null)
            gemini = JsonSerializer.Deserialize<RoomInfoCheckJob.GeminiRoomCheckResponse>(result.GeminiResult,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (gemini is not null)
            await UpdateDismissedDiffsAsync(result.RoomId, gemini, approved: []);

        result.Resolved = true;
        await _db.SaveChangesAsync();

        return View("Dismissed");
    }

    // 採用フィールドは削除、非採用フィールドは upsert
    private async Task UpdateDismissedDiffsAsync(
        int roomId,
        RoomInfoCheckJob.GeminiRoomCheckResponse gemini,
        List<string> approved)
    {
        var jsonOpt = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var candidates = new Dictionary<string, string?>();
        candidates["city"]             = gemini.City;
        candidates["can_whole"]        = gemini.CanWhole?.ToString();
        candidates["can_plasma"]       = gemini.CanPlasma?.ToString();
        candidates["can_platelet"]     = gemini.CanPlatelet?.ToString();
        candidates["closed_days"]      = gemini.ClosedDays;
        candidates["business_hours_0"] = gemini.BusinessHours?.FirstOrDefault(h => h.DayType == 0) is { } h0
                                            ? JsonSerializer.Serialize(h0, jsonOpt) : null;
        candidates["business_hours_1"] = gemini.BusinessHours?.FirstOrDefault(h => h.DayType == 1) is { } h1
                                            ? JsonSerializer.Serialize(h1, jsonOpt) : null;

        var existing = await _db.RoomDismissedDiffs
            .Where(d => d.RoomId == roomId)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;

        foreach (var (field, geminiValue) in candidates)
        {
            if (geminiValue is null) continue;

            if (approved.Contains(field))
            {
                var toDelete = existing.FirstOrDefault(d => d.Field == field);
                if (toDelete is not null) _db.RoomDismissedDiffs.Remove(toDelete);
            }
            else
            {
                var record = existing.FirstOrDefault(d => d.Field == field);
                if (record is null)
                {
                    _db.RoomDismissedDiffs.Add(new RoomDismissedDiff
                    {
                        RoomId      = roomId,
                        Field       = field,
                        GeminiValue = geminiValue,
                        DismissedAt = now,
                    });
                }
                else
                {
                    record.GeminiValue = geminiValue;
                    record.DismissedAt = now;
                }
            }
        }
    }

    // フォームの編集済み値から GeminiRoomCheckResponse を構築
    private static RoomInfoCheckJob.GeminiRoomCheckResponse BuildEffective(ApproveFormData form) =>
        new()
        {
            City        = NullIfBlank(form.City),
            CanWhole    = ParseNullableBool(form.CanWhole),
            CanPlasma   = ParseNullableBool(form.CanPlasma),
            CanPlatelet = ParseNullableBool(form.CanPlatelet),
            ClosedDays  = NullIfBlank(form.ClosedDays),
            BusinessHours =
            [
                new() {
                    DayType             = 0,
                    WholeReceptionStart = NullIfBlank(form.Bh0WholeStart),
                    WholeReceptionEnd   = NullIfBlank(form.Bh0WholeEnd),
                    WholeLunchStart     = NullIfBlank(form.Bh0WholeLunchStart),
                    WholeLunchEnd       = NullIfBlank(form.Bh0WholeLunchEnd),
                    CompReceptionStart  = NullIfBlank(form.Bh0CompStart),
                    CompReceptionEnd    = NullIfBlank(form.Bh0CompEnd),
                    CompLunchStart      = NullIfBlank(form.Bh0CompLunchStart),
                    CompLunchEnd        = NullIfBlank(form.Bh0CompLunchEnd),
                },
                new() {
                    DayType             = 1,
                    WholeReceptionStart = NullIfBlank(form.Bh1WholeStart),
                    WholeReceptionEnd   = NullIfBlank(form.Bh1WholeEnd),
                    WholeLunchStart     = NullIfBlank(form.Bh1WholeLunchStart),
                    WholeLunchEnd       = NullIfBlank(form.Bh1WholeLunchEnd),
                    CompReceptionStart  = NullIfBlank(form.Bh1CompStart),
                    CompReceptionEnd    = NullIfBlank(form.Bh1CompEnd),
                    CompLunchStart      = NullIfBlank(form.Bh1CompLunchStart),
                    CompLunchEnd        = NullIfBlank(form.Bh1CompLunchEnd),
                },
            ],
        };

    private static string?   NullIfBlank(string? s)     => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static bool?      ParseNullableBool(string? s) => s == "True" ? true : s == "False" ? false : null;
    private static TimeOnly? ParseTime(string? s)        => TimeOnly.TryParse(s, out var t) ? t : null;

    // フォームバインディング用モデル
    public class ApproveFormData
    {
        public List<string> Fields       { get; set; } = [];
        public string? City              { get; set; }
        public string? CanWhole          { get; set; }
        public string? CanPlasma         { get; set; }
        public string? CanPlatelet       { get; set; }
        public string? ClosedDays        { get; set; }
        public string? Bh0WholeStart     { get; set; }
        public string? Bh0WholeEnd       { get; set; }
        public string? Bh0WholeLunchStart { get; set; }
        public string? Bh0WholeLunchEnd  { get; set; }
        public string? Bh0CompStart      { get; set; }
        public string? Bh0CompEnd        { get; set; }
        public string? Bh0CompLunchStart { get; set; }
        public string? Bh0CompLunchEnd   { get; set; }
        public string? Bh1WholeStart     { get; set; }
        public string? Bh1WholeEnd       { get; set; }
        public string? Bh1WholeLunchStart { get; set; }
        public string? Bh1WholeLunchEnd  { get; set; }
        public string? Bh1CompStart      { get; set; }
        public string? Bh1CompEnd        { get; set; }
        public string? Bh1CompLunchStart { get; set; }
        public string? Bh1CompLunchEnd   { get; set; }
    }
}
