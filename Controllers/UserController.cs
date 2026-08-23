using KenketsuNote.Data;
using KenketsuNote.Services;
using Microsoft.AspNetCore.Mvc;

namespace KenketsuNote.Controllers;

public class UserController : Controller
{
    private readonly KenketsuNoteContext _db;

    public UserController(KenketsuNoteContext db)
    {
        _db = db;
    }

    // ハブページ（メニュー）
    [Route("u/{id}")]
    public IActionResult Hub(string id)
    {
        User? u = _db.Users.Find(id);
        if (u == null) return RedirectToAction("Index", "Home");

        u.LastAccessAt = DateTime.Now;
        _db.SaveChanges();

        ViewBag.UserId       = id;
        ViewBag.UserName     = u.UserName;
        ViewBag.Announcement = AnnouncementService.GetForUser(_db, u);
        return View();
    }

    /// <summary>お知らせモーダルの「次回から表示しない」</summary>
    [HttpPost]
    [Route("u/{id}/announcement/dismiss")]
    public IActionResult DismissAnnouncement(string id, [FromForm] int announcementId)
    {
        User? u = _db.Users.Find(id);
        if (u == null) return NotFound();

        u.DismissedAnnouncementId = announcementId;
        _db.SaveChanges();
        return Ok();
    }
}
