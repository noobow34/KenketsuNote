using Microsoft.AspNetCore.Mvc;

namespace KenketsuNote.Controllers;

[Route("pwa")]
public class PwaController : Controller
{
    [HttpGet("manifest")]
    public IActionResult Manifest(string userId, string page = "hub")
    {
        var (name, shortName, startUrl, description) = page switch
        {
            "stamp"   => ("けんけつノート 全国スタンプ", "全国スタンプ", $"/u/{userId}/stamp",   "全国の献血ルームをスタンプ帳で記録"),
            "tracker" => ("けんけつノート 計画管理",     "計画管理",     $"/u/{userId}/tracker", "献血の予定・実績・制限期間を管理"),
            _         => ("けんけつノート",              "けんけつノート", $"/u/{userId}",        "献血の記録をもっと楽しく"),
        };

        var manifest = new
        {
            name,
            short_name      = shortName,
            description,
            start_url       = startUrl,
            display         = "standalone",
            background_color = "#fafafa",
            theme_color     = "#d32f2f",
            lang            = "ja",
            icons           = new[]
            {
                new { src = "/image/logo1.png",     sizes = "192x192", type = "image/png", purpose = "any" },
                new { src = "/image/logo1-ogp.png", sizes = "512x512", type = "image/png", purpose = "any maskable" },
            },
        };

        return Json(manifest, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        });
    }
}
