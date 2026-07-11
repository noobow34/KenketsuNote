using Microsoft.AspNetCore.Mvc;

namespace KenketsuNote.Controllers;

public class ManualController : Controller
{
    [Route("manual")]
    public IActionResult Index()
    {
        return View();
    }
}
