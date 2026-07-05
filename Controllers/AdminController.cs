using KenketsuNote.Data;
using KenketsuNote.Infrastructure;
using KenketsuNote.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KenketsuNote.Controllers;

[Route("admin")]
public class AdminController : Controller
{
    private readonly KenketsuNoteContext _db;
    private readonly ISchedulerFactory _schedulerFactory;

    public AdminController(KenketsuNoteContext db, ISchedulerFactory schedulerFactory)
    {
        _db = db;
        _schedulerFactory = schedulerFactory;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        var checkResults = await _db.RoomCheckResults
            .Include(r => r.Room)
            .OrderBy(r => r.Resolved)
            .ThenByDescending(r => r.CheckedAt)
            .Take(100)
            .ToListAsync();

        var searchLogs = await _db.RoomSearchLogs
            .OrderByDescending(l => l.SearchedAt)
            .Take(50)
            .ToListAsync();

        ViewBag.CheckResults = checkResults;
        ViewBag.SearchLogs = searchLogs;
        return View();
    }

    [HttpPost("run-room-check")]
    public async Task<IActionResult> RunRoomCheck()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey("RoomInfoCheckJob");
            await scheduler.TriggerJob(jobKey);
            return Json(new { success = true, message = "ジョブを起動しました。結果はしばらく後にログに反映されます。" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"エラー: {ex.Message}" });
        }
    }

    [HttpPost("reload-master")]
    public IActionResult ReloadMaster()
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
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
