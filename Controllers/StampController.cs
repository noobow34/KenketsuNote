using KenketsuNote.Infrastructure;
using KenketsuNote.Dto;
using KenketsuNote.Data;
using KenketsuNote.Models;
using Microsoft.AspNetCore.Mvc;

namespace KenketsuNote.Controllers;

public class StampController : Controller
{
    private readonly KenketsuNoteContext _db;

    public StampController(KenketsuNoteContext db)
    {
        _db = db;
    }

    [Route("u/{id}/stamp")]
    public IActionResult Index(string id, [FromQuery(Name = "from")] string? from)
    {
        if (from == "ashiato")
        {
            var u = _db.Users.Find(id);
            if (u != null && !u.MigratedFromAshiato)
            {
                u.MigratedFromAshiato = true;
                _db.SaveChanges();
            }
            return Redirect($"/u/{id}/stamp");
        }
        return StampPage(id, false);
    }

    [Route("s/{id}")]
    public IActionResult Share(string id)
    {
        string? originalId = _db.ShareMappings
            .Where(sm => sm.ShareId == id)
            .Select(sm => sm.OriginalId)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(originalId))
            return RedirectToAction("Index", "Home");

        return StampPage(originalId, true, id);
    }

    [HttpPost]
    public string RegisterShareId(string originalId)
    {
        string generatedId = IdGenerator.Generate();
        while (_db.ShareMappings.Find(generatedId) != null)
        {
            generatedId = IdGenerator.Generate();
        }

        _db.ShareMappings.Add(new ShareMapping
        {
            ShareId    = generatedId,
            OriginalId = originalId
        });
        _db.SaveChanges();
        return generatedId;
    }

    [HttpPost]
    public IActionResult DeleteShareId(string shareId)
    {
        ShareMapping? sm = _db.ShareMappings.Find(shareId);
        if (sm != null)
        {
            _db.ShareMappings.Remove(sm);
            _db.SaveChanges();
        }
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SaveDisplayOrder([FromBody] SaveDisplayOrderRequest request)
    {
        var cOrders = _db.CenterBlockOrders.Where(cdo => cdo.UserId == request.UserId);
        _db.CenterBlockOrders.RemoveRange(cOrders);
        var pOrders = _db.PrefOrders.Where(pdo => pdo.UserId == request.UserId);
        _db.PrefOrders.RemoveRange(pOrders);

        foreach (var item in request.Regions!)
        {
            _db.CenterBlockOrders.Add(new CenterBlockOrder
            {
                UserId        = request.UserId!,
                CenterBlockId = item.CenterBlockId,
                DisplayOrder  = item.DisplayOrder
            });
            foreach (var pref in item.Prefectures!)
            {
                _db.PrefOrders.Add(new PrefOrder
                {
                    UserId       = request.UserId!,
                    PrefId       = pref.PrefId,
                    DisplayOrder = pref.DisplayOrder
                });
            }
        }

        _db.SaveChanges();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> ResetDisplayOrder(string userId)
    {
        _db.CenterBlockOrders.RemoveRange(_db.CenterBlockOrders.Where(cdo => cdo.UserId == userId));
        _db.PrefOrders.RemoveRange(_db.PrefOrders.Where(pdo => pdo.UserId == userId));
        _db.SaveChanges();
        return Ok();
    }

    [HttpPost]
    public IActionResult SaveShowClosedDefault(string userId, bool showClosed)
    {
        User? u = _db.Users.Find(userId);
        if (u == null) return NotFound();
        u.ShowClosedDefault = showClosed;
        _db.SaveChanges();
        return Ok();
    }

    // ── スタンプデータAPI ────────────────────────────────────────

    [HttpGet]
    public IActionResult GetAll(string userId)
    {
        var list = _db.VisitStamps
            .Where(v => v.UserId == userId)
            .Select(x => new { roomId = x.RoomId, date = x.VisitDate, angle = x.Angle })
            .ToList();
        return Json(list);
    }

    [HttpPost]
    public IActionResult Save(string userId, int roomId, string? date, double angle)
    {
        var existing = _db.VisitStamps.FirstOrDefault(x => x.RoomId == roomId && x.UserId == userId);
        var now = DateTime.Now;
        DateOnly? vd = string.IsNullOrEmpty(date) ? null : DateOnly.Parse(date);

        if (existing == null)
        {
            _db.VisitStamps.Add(new VisitStamp
            {
                RoomId    = roomId,
                UserId    = userId,
                VisitDate = vd,
                Angle     = angle,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.UpdatedAt = now;
            existing.VisitDate = vd;
        }

        _db.SaveChanges();
        return Json(new { ok = true });
    }

    [HttpPost]
    public IActionResult DeleteStamp(string userId, int roomId)
    {
        var item = _db.VisitStamps.FirstOrDefault(x => x.RoomId == roomId && x.UserId == userId);
        if (item != null)
        {
            _db.VisitStamps.Remove(item);
            _db.SaveChanges();
        }
        return Json(new { ok = true });
    }

    [HttpPost]
    public IActionResult Reset(string userId)
    {
        _db.VisitStamps.RemoveRange(_db.VisitStamps.Where(v => v.UserId == userId));
        _db.SaveChanges();
        return Json(new { ok = true });
    }

    // ── プライベート ─────────────────────────────────────────────

    private IActionResult StampPage(string id, bool isShare, string fromShareId = "")
    {
        User? u = _db.Users.Find(id);
        if (u == null) return RedirectToAction("Index", "Home");

        u.LastAccessAt = DateTime.Now;
        _db.SaveChanges();

        var model = new StampModel
        {
            User             = u,
            Rooms            = MasterData.Rooms,
            IsShare          = isShare,
            ShowClosedDefault = u.ShowClosedDefault
        };

        int[] roomsPref = model.Rooms.Select(r => r.PrefId).Distinct().ToArray();
        var pOrders = _db.PrefOrders
            .Where(po => po.UserId == id && roomsPref.Contains(po.PrefId))
            .ToDictionary(po => po.PrefId, po => po.DisplayOrder);

        model.Prefectures = MasterData.Prefectures.Select(p => new Pref
        {
            PrefId        = p.PrefId,
            DisplayOrder  = p.DisplayOrder,
            CenterBlockId = p.CenterBlockId,
            PrefName      = p.PrefName
        }).ToArray();
        if (pOrders.Count != 0)
        {
            foreach (var pref in model.Prefectures)
                pref.DisplayOrder = pOrders.GetValueOrDefault(pref.PrefId, pref.DisplayOrder ?? 0);
        }

        int[] roomsCenterBlock = model.Prefectures.Select(p => p.CenterBlockId).Distinct().ToArray();
        var cOrders = _db.CenterBlockOrders
            .Where(co => co.UserId == id && roomsCenterBlock.Contains(co.CenterBlockId))
            .ToDictionary(co => co.CenterBlockId, co => co.DisplayOrder);

        model.CenterBlocks = MasterData.CenterBlocks.Select(cb => new CenterBlock
        {
            CenterBlockId   = cb.CenterBlockId,
            CenterBlockName = cb.CenterBlockName,
            DisplayOrder    = cb.DisplayOrder
        }).ToArray();
        if (cOrders.Count != 0)
        {
            foreach (var cb in model.CenterBlocks)
                cb.DisplayOrder = cOrders.GetValueOrDefault(cb.CenterBlockId, cb.DisplayOrder ?? 0);
        }

        model.ShareId = string.IsNullOrEmpty(fromShareId)
            ? _db.ShareMappings.Where(sm => sm.OriginalId == id).FirstOrDefault()?.ShareId ?? string.Empty
            : isShare ? fromShareId : string.Empty;

        return View("Index", model);
    }
}
