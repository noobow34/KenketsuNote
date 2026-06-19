using Microsoft.AspNetCore.Mvc;

namespace KenketsuNote.Controllers;

public class ManualController : Controller
{
    [Route("u/{userId}/manual")]
    public IActionResult Index(string userId)
    {
        ViewBag.UserId = userId;
        return View();
    }
}
