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

    private const int CheckPageSize = 20;
    private const int LogPageSize   = 50;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] int checkPage = 1, [FromQuery] int logPage = 1)
    {
        if (!AdminAuth.IsAdmin(HttpContext)) return NotFound();

        checkPage = Math.Max(1, checkPage);
        logPage   = Math.Max(1, logPage);

        var checkTotal = await _db.RoomCheckResults.CountAsync();
        var checkResults = await _db.RoomCheckResults
            .Include(r => r.Room)
            .OrderBy(r => r.Resolved)
            .ThenByDescending(r => r.CheckedAt)
            .Skip((checkPage - 1) * CheckPageSize)
            .Take(CheckPageSize)
            .ToListAsync();

        var logTotal = await _db.RoomSearchLogs.CountAsync();
        var searchLogs = await _db.RoomSearchLogs
            .OrderByDescending(l => l.SearchedAt)
            .Skip((logPage - 1) * LogPageSize)
            .Take(LogPageSize)
            .ToListAsync();

        ViewBag.CheckResults  = checkResults;
        ViewBag.CheckPage     = checkPage;
        ViewBag.CheckTotalPages = (int)Math.Ceiling(checkTotal / (double)CheckPageSize);

        ViewBag.SearchLogs  = searchLogs;
        ViewBag.LogPage     = logPage;
        ViewBag.LogTotalPages = (int)Math.Ceiling(logTotal / (double)LogPageSize);

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
