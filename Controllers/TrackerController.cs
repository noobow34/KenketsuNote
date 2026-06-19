using KenketsuNote.Infrastructure;
using KenketsuNote.Data;
using KenketsuNote.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KenketsuNote.Controllers;

public class TrackerController : Controller
{
    private readonly KenketsuNoteContext _db;

    private static readonly string[] ValidTypes = ["whole_200", "whole_400", "plasma", "platelet"];

    public TrackerController(KenketsuNoteContext db)
    {
        _db = db;
    }

    private static KenketsuLimitService LimitFor(string? gender)
        => new(KenketsuLimitService.WholeMaxMlForGender(gender));

    private string? GetGender(string userId)
        => _db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.Gender).FirstOrDefault();

    // ─────────────────────────────────────────────
    // 画面表示
    // ─────────────────────────────────────────────
    [Route("u/{userId}/tracker")]
    public IActionResult Index(string userId, int? year, int? month)
    {
        var u = _db.Users.Find(userId);
        if (u == null) return RedirectToAction("Index", "Home");

        var today = DateOnly.FromDateTime(DateTime.Now);
        ViewBag.UserId         = userId;
        ViewBag.Year           = year  ?? today.Year;
        ViewBag.Month          = month ?? today.Month;
        ViewBag.Today          = today.ToString("yyyy-MM-dd");
        ViewBag.GenderRequired = u.Gender == null;

        // ルームデータをJSON化してViewに渡す
        var rooms = MasterData.Rooms.Select(r => new
        {
            r.RoomId,
            r.RoomName,
            r.PrefId,
            r.IsClosed,
        });
        var prefs = MasterData.Prefectures.Select(p => new
        {
            p.PrefId,
            p.PrefName,
            p.CenterBlockId,
        });
        var blocks = MasterData.CenterBlocks.Select(b => new
        {
            b.CenterBlockId,
            b.CenterBlockName,
        });
        var jsonOpt = new System.Text.Json.JsonSerializerOptions
            { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        ViewBag.RoomsJson  = System.Text.Json.JsonSerializer.Serialize(rooms,  jsonOpt);
        ViewBag.PrefsJson  = System.Text.Json.JsonSerializer.Serialize(prefs,  jsonOpt);
        ViewBag.BlocksJson = System.Text.Json.JsonSerializer.Serialize(blocks, jsonOpt);
        return View();
    }

    // ─────────────────────────────────────────────
    // Ajax: 月データ取得
    // ─────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetMonthData(string userId, int year, int month)
    {
        var from = new DateOnly(year, month, 1).AddDays(-6);
        var to   = new DateOnly(year, month, 1).AddMonths(1).AddDays(6);

        var gender = GetGender(userId);
        var limit  = LimitFor(gender);

        var records = _db.KenketsuRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.DonationDate >= from && r.DonationDate <= to)
            .OrderBy(r => r.DonationDate)
            .ToList();

        var restrictions = _db.KenketsuRestrictions.AsNoTracking()
            .Where(r => r.UserId == userId).ToList();

        var allRecords     = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var intervalRanges = limit.GetIntervalConstrainedRanges(from, to, allRecords);
        var limitRanges    = limit.GetLimitConstrainedRanges(from, to, allRecords);

        var restrictionRanges = restrictions
            .Where(r => r.EndDate >= from && r.StartDate <= to)
            .Select(r =>
            {
                var effectiveStart = allRecords.Any(d => d.DonationDate == r.StartDate)
                    ? r.StartDate.AddDays(1) : r.StartDate;
                var dispStart = effectiveStart < from ? from : effectiveStart;
                return new
                {
                    Start       = dispStart.ToString("yyyy-MM-dd"),
                    End         = (r.EndDate > to ? to : r.EndDate).ToString("yyyy-MM-dd"),
                    r.Reason,
                    r.Id,
                    Kind        = "restriction",
                    IsEffective = effectiveStart <= r.EndDate,
                };
            })
            .Where(r => r.IsEffective && string.Compare(r.Start, r.End) <= 0)
            .ToList();

        return Json(new
        {
            records = records.Select(r => new
            {
                r.Id,
                DonationDate = r.DonationDate.ToString("yyyy-MM-dd"),
                r.DonationType,
                r.RecordType,
                r.VolumeMl,
                r.ComponentCount,
                r.Notes,
                r.RoomId,
            }),
            intervalRanges = intervalRanges.Select(r => new
            {
                Start = r.Start.ToString("yyyy-MM-dd"),
                End   = r.End.ToString("yyyy-MM-dd"),
                r.Kind,
            }),
            limitRanges = limitRanges.Select(r => new
            {
                Start = r.Start.ToString("yyyy-MM-dd"),
                End   = r.End.ToString("yyyy-MM-dd"),
                r.Kind,
            }),
            restrictionRanges,
        });
    }

    // ─────────────────────────────────────────────
    // Ajax: サマリー取得
    // ─────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetSummary(string userId)
    {
        var today        = DateOnly.FromDateTime(DateTime.Now);
        var gender       = GetGender(userId);
        var limit        = LimitFor(gender);
        var records      = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var restrictions = _db.KenketsuRestrictions.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var s = limit.CalculateSummary(today, records, restrictions);

        return Json(new
        {
            s.UsedVolumeMl,
            s.MaxVolumeMl,
            s.RemainingMl,
            s.UsedComponentCount,
            s.MaxComponentCount,
            s.RemainingCount,
            NextWholePossible                  = s.NextWholePossible?.ToString("yyyy/MM/dd"),
            NextComponentPossible              = s.NextComponentPossible?.ToString("yyyy/MM/dd"),
            s.NextWholeLimitConstrained,
            s.NextComponentLimitConstrained,
            s.NextWholeRestrictionConstrained,
            s.NextComponentRestrictionConstrained,
            genderRequired = gender == null,
            activeRestrictions = s.ActiveRestrictions.Select(r =>
            {
                var effStart = records.Any(d => d.DonationDate == r.StartDate)
                    ? r.StartDate.AddDays(1) : r.StartDate;
                return new
                {
                    r.Id,
                    Start      = effStart.ToString("yyyy/MM/dd"),
                    End        = r.EndDate.ToString("yyyy/MM/dd"),
                    r.Reason,
                    IsActive   = effStart <= r.EndDate && r.EndDate >= today,
                };
            }).Where(r => r.IsActive),
        });
    }

    // ─────────────────────────────────────────────
    // Ajax: 過去1年の実績・予定一覧
    // ─────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetRecentRecords(string userId)
    {
        var from = DateOnly.FromDateTime(DateTime.Now).AddYears(-1);

        var records = _db.KenketsuRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.DonationDate >= from)
            .OrderByDescending(r => r.DonationDate)
            .ToList();

        return Json(records.Select(r => new
        {
            r.Id,
            DonationDate = r.DonationDate.ToString("yyyy/MM/dd"),
            r.DonationType,
            r.RecordType,
            r.Notes,
        }));
    }

    // ─────────────────────────────────────────────
    // 性別設定
    // ─────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SetGender(string userId, string gender)
    {
        if (gender != "male" && gender != "female")
            return Json(new { success = false, message = "性別の値が不正です。" });

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return Json(new { success = false, message = "ユーザーが見つかりません。" });

        user.Gender = gender;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ─────────────────────────────────────────────
    // 予定追加
    // ─────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> AddPlan(string userId, string donationDate, string donationType, string? notes, int? roomId, string? recordType)
    {
        if (!TryParseDate(donationDate, out var date))
            return Json(new { success = false, message = "日付が不正です。" });
        if (!ValidTypes.Contains(donationType))
            return Json(new { success = false, message = "種別が不正です。" });

        var rtype = recordType == "actual" ? "actual" : "plan";

        var gender = GetGender(userId);
        if (gender == null)
            return Json(new { success = false, requiresGender = true, message = "計画管理を利用するには性別を設定してください。" });

        var limit        = LimitFor(gender);
        var records      = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var restrictions = _db.KenketsuRestrictions.AsNoTracking().Where(r => r.UserId == userId).ToList();

        var v = limit.Validate(date, donationType, records, restrictions);
        if (!v.IsValid) return Json(new { success = false, message = v.ErrorMessage });

        var rec = BuildRecord(userId, date, donationType, rtype, notes);
        rec.RoomId = roomId;
        _db.KenketsuRecords.Add(rec);
        await _db.SaveChangesAsync();

        if (rtype == "actual")
        {
            var canStamp = rec.RoomId.HasValue &&
                           !_db.VisitStamps.Any(s => s.UserId == userId && s.RoomId == rec.RoomId);
            return Json(new {
                success  = true,
                canStamp,
                recordId = rec.Id,
                roomId   = rec.RoomId,
                roomName = rec.RoomId.HasValue
                    ? MasterData.Rooms.FirstOrDefault(r => r.RoomId == rec.RoomId)?.RoomName
                    : null
            });
        }

        return Json(new { success = true });
    }

    // ─────────────────────────────────────────────
    // 予定→実績変換
    // ─────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ConvertToActual(string userId, int id, string donationType, string? notes, int? roomId)
    {
        if (!ValidTypes.Contains(donationType))
            return Json(new { success = false, message = "種別が不正です。" });

        var gender = GetGender(userId);
        if (gender == null)
            return Json(new { success = false, requiresGender = true, message = "計画管理を利用するには性別を設定してください。" });

        var record = await _db.KenketsuRecords.FindAsync(id);
        if (record == null || record.UserId != userId)
            return Json(new { success = false, message = "レコードが見つかりません。" });

        var limit        = LimitFor(gender);
        var records      = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var restrictions = _db.KenketsuRestrictions.AsNoTracking().Where(r => r.UserId == userId).ToList();

        var v = limit.Validate(record.DonationDate, donationType, records, restrictions, excludeId: id);
        if (!v.IsValid) return Json(new { success = false, message = v.ErrorMessage });

        record.DonationType   = donationType;
        record.RecordType     = "actual";
        record.VolumeMl       = GetVolumeMl(donationType);
        record.ComponentCount = GetComponentCount(donationType);
        record.Notes          = notes ?? record.Notes;
        if (roomId.HasValue) record.RoomId = roomId;
        record.UpdatedAt      = DateTime.Now;
        await _db.SaveChangesAsync();

        var allRecords = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var proposals  = limit.CalculateReschedule(record, allRecords, restrictions);

        // スタンプ反映が可能かどうかフラグを返す
        var canStamp = record.RoomId.HasValue &&
                       !_db.VisitStamps.Any(s => s.UserId == userId && s.RoomId == record.RoomId);

        return Json(new
        {
            success = true,
            canStamp,
            recordId = record.Id,
            roomId   = record.RoomId,
            roomName = record.RoomId.HasValue
                ? MasterData.Rooms.FirstOrDefault(r => r.RoomId == record.RoomId)?.RoomName
                : null,
            proposals = proposals.Select(p => new
            {
                p.RecordId,
                p.DonationType,
                OriginalDate = p.OriginalDate.ToString("yyyy/MM/dd"),
                NewDate      = p.NewDate.ToString("yyyy/MM/dd"),
            }),
        });
    }

    // ─────────────────────────────────────────────
    // 再スケジュール適用
    // ─────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ApplyReschedule(string userId, string proposals)
    {
        List<RescheduleItem>? items;
        try
        {
            items = System.Text.Json.JsonSerializer.Deserialize<List<RescheduleItem>>(proposals,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return Json(new { success = false, message = "パラメータが不正です。" }); }

        if (items == null || items.Count == 0) return Json(new { success = true });

        foreach (var item in items)
        {
            var record = await _db.KenketsuRecords.FindAsync(item.RecordId);
            if (record == null || record.UserId != userId) continue;
            if (!TryParseDate(item.NewDate, out var newDate)) continue;
            record.DonationDate = newDate;
            record.UpdatedAt    = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ─────────────────────────────────────────────
    // 編集
    // ─────────────────────────────────────────────
    // ─────────────────────────────────────────────
    // スタンプ反映
    // ─────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ApplyStamp(string userId, int recordId)
    {
        var record = await _db.KenketsuRecords.FindAsync(recordId);
        if (record == null || record.UserId != userId || !record.RoomId.HasValue)
            return Json(new { success = false, message = "記録が見つかりません。" });

        var existing = _db.VisitStamps.FirstOrDefault(s => s.UserId == userId && s.RoomId == record.RoomId);
        if (existing != null)
        {
            // 既存スタンプの訪問日を更新（より新しい日付に）
            if (!existing.VisitDate.HasValue || record.DonationDate > existing.VisitDate)
            {
                existing.VisitDate = record.DonationDate;
                existing.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
            return Json(new { success = true, updated = true });
        }

        _db.VisitStamps.Add(new VisitStamp
        {
            UserId    = userId,
            RoomId    = record.RoomId.Value,
            VisitDate = record.DonationDate,
            Angle     = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        });
        await _db.SaveChangesAsync();
        return Json(new { success = true, updated = false });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRecord(string userId, int id, string donationDate, string donationType, string? notes, int? roomId)
    {
        if (!TryParseDate(donationDate, out var date))
            return Json(new { success = false, message = "日付が不正です。" });
        if (!ValidTypes.Contains(donationType))
            return Json(new { success = false, message = "種別が不正です。" });

        var gender = GetGender(userId);
        if (gender == null)
            return Json(new { success = false, requiresGender = true, message = "計画管理を利用するには性別を設定してください。" });

        var record = await _db.KenketsuRecords.FindAsync(id);
        if (record == null || record.UserId != userId)
            return Json(new { success = false, message = "レコードが見つかりません。" });

        var limit        = LimitFor(gender);
        var records      = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var restrictions = _db.KenketsuRestrictions.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var v = limit.Validate(date, donationType, records, restrictions, excludeId: id);
        if (!v.IsValid) return Json(new { success = false, message = v.ErrorMessage });

        record.DonationDate   = date;
        record.DonationType   = donationType;
        record.VolumeMl       = GetVolumeMl(donationType);
        record.ComponentCount = GetComponentCount(donationType);
        record.Notes          = notes;
        record.RoomId         = roomId;
        record.UpdatedAt      = DateTime.Now;
        await _db.SaveChangesAsync();

        var allRecords = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var proposals  = limit.CalculateReschedule(record, allRecords, restrictions);

        return Json(new
        {
            success = true,
            proposals = proposals.Select(p => new
            {
                p.RecordId,
                p.DonationType,
                OriginalDate = p.OriginalDate.ToString("yyyy/MM/dd"),
                NewDate      = p.NewDate.ToString("yyyy/MM/dd"),
            }),
        });
    }

    // ─────────────────────────────────────────────
    // 削除
    // ─────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> DeleteRecord(string userId, int id)
    {
        var gender = GetGender(userId);
        if (gender == null)
            return Json(new { success = false, requiresGender = true, message = "計画管理を利用するには性別を設定してください。" });

        var record = await _db.KenketsuRecords.FindAsync(id);
        if (record == null || record.UserId != userId)
            return Json(new { success = false, message = "レコードが見つかりません。" });

        _db.KenketsuRecords.Remove(record);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> BulkDeleteRecords(string userId, string ids)
    {
        List<int>? idList;
        try { idList = System.Text.Json.JsonSerializer.Deserialize<List<int>>(ids); }
        catch { return Json(new { success = false, message = "パラメータが不正です。" }); }

        if (idList == null || idList.Count == 0) return Json(new { success = true });

        var records = _db.KenketsuRecords
            .Where(r => r.UserId == userId && idList.Contains(r.Id) && r.RecordType == "plan")
            .ToList();

        _db.KenketsuRecords.RemoveRange(records);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ─────────────────────────────────────────────
    // 次回可能日（ホバー用）
    // ─────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetNextPossible(string userId, int id)
    {
        var record = _db.KenketsuRecords.AsNoTracking().FirstOrDefault(r => r.Id == id && r.UserId == userId);
        if (record == null) return Json(new { success = false });

        var gender       = GetGender(userId);
        var limit        = LimitFor(gender);
        var allRecords   = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
        var restrictions = _db.KenketsuRestrictions.AsNoTracking().Where(r => r.UserId == userId).ToList();

        var nextWhole = TryNextDate(limit, "whole_400", record.DonationDate, allRecords, restrictions);
        var nextComp  = TryNextDate(limit, "plasma",    record.DonationDate, allRecords, restrictions);

        return Json(new
        {
            success                   = true,
            nextWhole                 = nextWhole?.Date.ToString("yyyy/MM/dd"),
            nextComponent             = nextComp?.Date.ToString("yyyy/MM/dd"),
            nextWholeLimitConstrained = nextWhole?.LimitConstrained       ?? false,
            nextCompLimitConstrained  = nextComp?.LimitConstrained        ?? false,
            nextWholeRestConstrained  = nextWhole?.RestrictionConstrained ?? false,
            nextCompRestConstrained   = nextComp?.RestrictionConstrained  ?? false,
        });
    }

    // ─────────────────────────────────────────────
    // 手動制限 CRUD
    // ─────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> AddRestriction(string userId, string startDate, int durationDays, string? reason)
    {
        if (!TryParseDate(startDate, out var start))
            return Json(new { success = false, message = "日付が不正です。" });
        if (durationDays < 1 || durationDays > 365)
            return Json(new { success = false, message = "日数は1〜365で指定してください。" });

        var gender = GetGender(userId);
        if (gender == null)
            return Json(new { success = false, requiresGender = true, message = "計画管理を利用するには性別を設定してください。" });

        var limit   = LimitFor(gender);
        var endDate = start.AddDays(durationDays - 1);
        var affected = _db.KenketsuRecords.AsNoTracking()
            .Where(r => r.UserId == userId && r.RecordType == "plan" && r.DonationDate >= start && r.DonationDate <= endDate)
            .OrderBy(r => r.DonationDate)
            .ToList();

        _db.KenketsuRestrictions.Add(new KenketsuRestriction
        {
            UserId       = userId,
            StartDate    = start,
            DurationDays = durationDays,
            Reason       = reason,
        });
        await _db.SaveChangesAsync();

        if (affected.Count > 0)
        {
            var allRecords   = _db.KenketsuRecords.AsNoTracking().Where(r => r.UserId == userId).ToList();
            var restrictions = _db.KenketsuRestrictions.AsNoTracking().Where(r => r.UserId == userId).ToList();
            var triggerBase  = allRecords.Where(r => r.DonationDate < start).MaxBy(r => r.DonationDate);

            if (triggerBase != null)
            {
                var proposals = limit.CalculateReschedule(triggerBase, allRecords, restrictions);
                return Json(new
                {
                    success = true,
                    proposals = proposals.Select(p => new
                    {
                        p.RecordId,
                        p.DonationType,
                        OriginalDate = p.OriginalDate.ToString("yyyy/MM/dd"),
                        NewDate      = p.NewDate.ToString("yyyy/MM/dd"),
                    }),
                });
            }
        }

        return Json(new { success = true, proposals = Array.Empty<object>() });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRestriction(string userId, int id, string startDate, int durationDays, string? reason)
    {
        if (!TryParseDate(startDate, out var start))
            return Json(new { success = false, message = "日付が不正です。" });
        if (durationDays < 1 || durationDays > 365)
            return Json(new { success = false, message = "日数は1〜365で指定してください。" });

        var gender = GetGender(userId);
        if (gender == null)
            return Json(new { success = false, requiresGender = true, message = "計画管理を利用するには性別を設定してください。" });

        var r = await _db.KenketsuRestrictions.FindAsync(id);
        if (r == null || r.UserId != userId)
            return Json(new { success = false, message = "見つかりません。" });

        r.StartDate    = start;
        r.DurationDays = durationDays;
        r.Reason       = reason;
        r.UpdatedAt    = DateTime.Now;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteRestriction(string userId, int id)
    {
        var gender = GetGender(userId);
        if (gender == null)
            return Json(new { success = false, requiresGender = true, message = "計画管理を利用するには性別を設定してください。" });

        var r = await _db.KenketsuRestrictions.FindAsync(id);
        if (r == null || r.UserId != userId)
            return Json(new { success = false, message = "見つかりません。" });

        _db.KenketsuRestrictions.Remove(r);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ─────────────────────────────────────────────
    // ヘルパー
    // ─────────────────────────────────────────────
    private static bool TryParseDate(string? s, out DateOnly date)
    {
        date = default;
        return !string.IsNullOrEmpty(s) &&
               DateOnly.TryParseExact(s, ["yyyy-MM-dd", "yyyy/MM/dd"],
                   null, System.Globalization.DateTimeStyles.None, out date);
    }

    private static (DateOnly Date, bool LimitConstrained, bool RestrictionConstrained)? TryNextDate(
        KenketsuLimitService limit,
        string donationType, DateOnly baseDate,
        IReadOnlyList<KenketsuRecord> records,
        IReadOnlyList<KenketsuRestriction> restrictions)
    {
        try
        {
            return limit.EarliestPossibleDateWithReason(
                baseDate.AddDays(1), donationType, records, restrictions);
        }
        catch { return null; }
    }

    private static KenketsuRecord BuildRecord(
        string userId, DateOnly date, string donationType, string recordType, string? notes) => new()
    {
        UserId         = userId,
        DonationDate   = date,
        DonationType   = donationType,
        RecordType     = recordType,
        VolumeMl       = GetVolumeMl(donationType),
        ComponentCount = GetComponentCount(donationType),
        Notes          = notes,
    };

    private static int? GetVolumeMl(string t) => t switch
    {
        "whole_200" => 200,
        "whole_400" => 400,
        _           => null,
    };

    private static int? GetComponentCount(string t) => t switch
    {
        "plasma"   => 1,
        "platelet" => 2,
        _          => null,
    };
}

public record RescheduleItem(int RecordId, string NewDate);
