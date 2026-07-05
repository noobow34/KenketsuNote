using System.Text.Json;
using KenketsuNote.Auth;
using KenketsuNote.Data;
using KenketsuNote.Infrastructure;
using KenketsuNote.Jobs;
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

        var jsonOpt = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        ViewBag.RoomsJson  = JsonSerializer.Serialize(MasterData.Rooms.Where(r => !r.IsClosed).Select(r => new { r.RoomId, r.RoomName, r.PrefId, r.IsClosed }), jsonOpt);
        ViewBag.PrefsJson  = JsonSerializer.Serialize(MasterData.Prefectures.Select(p => new { p.PrefId, p.PrefName, p.CenterBlockId }), jsonOpt);
        ViewBag.BlocksJson = JsonSerializer.Serialize(MasterData.CenterBlocks.Select(b => new { b.CenterBlockId, b.CenterBlockName }), jsonOpt);

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

    [HttpPost("run-room-check-single")]
    public async Task<IActionResult> RunRoomCheckSingle([FromForm] int roomId)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();
        try
        {
            var room = await _db.KenketsuRooms
                .Include(r => r.BusinessHours)
                .FirstOrDefaultAsync(r => r.RoomId == roomId);
            if (room is null) return Json(new { success = false, message = "ルームが見つかりません。" });
            if (room.RoomUrl is null) return Json(new { success = false, message = "このルームには公式URLが登録されていません。" });

            var geminiApiKey  = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
            var slackBotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
            var slackChannel  = Environment.GetEnvironmentVariable("SLACK_ROOM_CHECK_CHANNEL") ?? "";
            var baseUrl       = (Environment.GetEnvironmentVariable("KENKETSUNOTE_BASE_URL") ?? "").TrimEnd('/');

            if (string.IsNullOrEmpty(geminiApiKey))
                return Json(new { success = false, message = "GEMINI_API_KEY が設定されていません。" });

            await RoomInfoCheckJob.ProcessRoomAsync(room, _db, geminiApiKey, slackBotToken, slackChannel, baseUrl);
            return Json(new { success = true, message = $"「{room.RoomName}」のチェックが完了しました。ログを確認してください。" });
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
