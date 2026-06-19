using KenketsuNote.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace KenketsuNote.Controllers;

public class AdminController : Controller
{
    private static string? AdminKey =>
        Environment.GetEnvironmentVariable("KENKETSUNOTE_ADMIN_KEY");

    private bool IsAuthorized(string? key) =>
        !string.IsNullOrEmpty(AdminKey) && key == AdminKey;

    // ─────────────────────────────────────────────
    // 管理画面
    // ─────────────────────────────────────────────
    [HttpGet("admin")]
    public IActionResult Index(string? key)
    {
        if (!IsAuthorized(key)) return Forbid();
        ViewBag.Key = key;
        return View();
    }

    // ─────────────────────────────────────────────
    // マスタデータ再ロード
    // ─────────────────────────────────────────────
    [HttpPost("admin/reload-master")]
    public IActionResult ReloadMaster([FromForm] string? key)
    {
        if (!IsAuthorized(key))
            return Json(new { success = false, message = "認証エラー" });

        try
        {
            MasterData.Load();
            return Json(new
            {
                success = true,
                message = $"マスタデータを再ロードしました（ルーム {MasterData.Rooms.Length} 件 / 都道府県 {MasterData.Prefectures.Length} 件 / ブロック {MasterData.CenterBlocks.Length} 件）"
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"エラー: {ex.Message}" });
        }
    }
}
