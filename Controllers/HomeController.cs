using KenketsuNote.Infrastructure;
using KenketsuNote.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KenketsuNote.Controllers;

public class HomeController : Controller
{
    private readonly KenketsuNoteContext _db;

    public HomeController(KenketsuNoteContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        ViewBag.ChangeLogs = _db.ChangeLogs
            .AsNoTracking()
            .OrderByDescending(c => c.ReleasedOn)
            .ThenByDescending(c => c.Id)
            .ToList();
        return View();
    }

    [Route("CheckUser")]
    public IActionResult CheckUser(string userId)
    {
        var exists = !string.IsNullOrWhiteSpace(userId)
                     && _db.Users.Any(u => u.UserId == userId);
        return Json(new { exists });
    }

    [Route("Register")]
    public IActionResult Register(string? userName, string? gender)
    {
        string generatedId = IdGenerator.Generate();
        while (_db.Users.Find(generatedId) != null)
        {
            generatedId = IdGenerator.Generate();
        }

        User newUser = new()
        {
            UserId       = generatedId,
            UserName     = userName,
            Gender       = gender == "male" || gender == "female" ? gender : null,
            RegisteredAt = DateTime.Now,
            LastAccessAt = DateTime.Now
        };
        _db.Users.Add(newUser);
        _db.SaveChanges();

        return RedirectToAction("Hub", "User", new { id = generatedId });
    }
}
