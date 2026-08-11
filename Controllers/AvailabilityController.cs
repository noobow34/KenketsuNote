using Microsoft.AspNetCore.Mvc;

namespace KenketsuNote.Controllers;

/// <summary>
/// PC用アプリ「献血空き横断検索」の案内ページ。
/// 注意事項に同意した人だけがダウンロードへ進めるようにしている。
/// </summary>
public class AvailabilityController : Controller
{
    [Route("availability")]
    public IActionResult Index()
    {
        return View();
    }
}
