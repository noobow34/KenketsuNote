using KenketsuNote.Data;
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

        ViewBag.UserId   = id;
        ViewBag.UserName = u.UserName;
        return View();
    }
}
