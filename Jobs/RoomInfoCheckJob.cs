using System.Text;
using System.Text.Json;
using KenketsuNote.Data;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KenketsuNote.Jobs;

public class RoomInfoCheckJob : IJob
{
    // 1回の実行でチェックするルーム数
    private const int BatchSize = 10;
    // ルーム間の待機（ms）
    private const int DelayBetweenRooms = 3000;
    // レート制限時のリトライ待機（ms）
    private const int RateLimitRetryDelay = 65000;
    private const int MaxRetry = 3;

    private static readonly string GeminiApiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent";
    private static readonly string SlackApiUrl = "https://slack.com/api/chat.postMessage";

    public async Task Execute(IJobExecutionContext context)
    {
        var geminiApiKey   = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
        var slackBotToken  = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
        var slackChannel   = Environment.GetEnvironmentVariable("SLACK_ROOM_CHECK_CHANNEL") ?? "";
        var baseUrl        = (Environment.GetEnvironmentVariable("KENKETSUNOTE_BASE_URL") ?? "").TrimEnd('/');
        if (string.IsNullOrEmpty(geminiApiKey))
        {
            Console.WriteLine("[RoomInfoCheckJob] GEMINI_API_KEY が設定されていません");
            return;
        }

        var services = context.Scheduler.Context["services"] as IServiceProvider
            ?? throw new InvalidOperationException("IServiceProvider がスケジューラコンテキストに登録されていません");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KenketsuNoteContext>();

        // ── ジョブ状態取得（なければ初期化） ──────────────
        var state = await db.RoomCheckJobStates.FindAsync(1);
        if (state is null)
        {
            state = new RoomCheckJobState { Id = 1, NextOffset = 0 };
            db.RoomCheckJobStates.Add(state);
            await db.SaveChangesAsync();
        }

        // ── バッチ対象ルームを取得 ─────────────────────────
        var allRoomIds = await db.KenketsuRooms
            .Where(r => !r.IsClosed && r.RoomUrl != null)
            .OrderBy(r => r.RoomId)
            .Select(r => r.RoomId)
            .ToListAsync();

        if (allRoomIds.Count == 0) return;

        // オフセットが末尾を超えていたらリセット
        if (state.NextOffset >= allRoomIds.Count)
            state.NextOffset = 0;

        var batchIds = allRoomIds
            .Skip(state.NextOffset)
            .Take(BatchSize)
            .ToList();

        var rooms = await db.KenketsuRooms
            .Include(r => r.BusinessHours)
            .Where(r => batchIds.Contains(r.RoomId))
            .ToListAsync();

        Console.WriteLine($"[RoomInfoCheckJob] offset={state.NextOffset} 対象={rooms.Count}件");

        foreach (var room in rooms.OrderBy(r => r.RoomId))
        {
            try
            {
                await ProcessRoomAsync(room, db, geminiApiKey, slackBotToken, slackChannel, baseUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomInfoCheckJob] エラー ({room.RoomName}): {ex.Message}");
            }

            await Task.Delay(DelayBetweenRooms);
        }

        // ── オフセット更新 ────────────────────────────────
        state.NextOffset = (state.NextOffset + batchIds.Count) % allRoomIds.Count;
        state.LastRunAt  = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        Console.WriteLine($"[RoomInfoCheckJob] 完了。次回offset={state.NextOffset}");
    }

    // ── 1ルーム分の処理（バッチ・単体実行共通） ──────────
    public static async Task ProcessRoomAsync(
        KenketsuRoom room, KenketsuNoteContext db,
        string geminiApiKey, string slackBotToken, string slackChannel, string baseUrl)
    {
        Console.WriteLine($"[RoomInfoCheckJob] チェック開始: {room.RoomName} ({room.RoomUrl})");

        var dismissedDiffs = await db.RoomDismissedDiffs
            .Where(d => d.RoomId == room.RoomId)
            .ToListAsync();

        var job = new RoomInfoCheckJob();
        var geminiResult = await job.CallGeminiWithRetryAsync(geminiApiKey, room, room.RoomUrl!, dismissedDiffs);
        if (geminiResult is null) return;

        var result = new RoomCheckResult
        {
            RoomId       = room.RoomId,
            CheckedAt    = DateTimeOffset.UtcNow,
            GeminiResult = JsonSerializer.Serialize(geminiResult, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            HasChanges   = geminiResult.HasChanges,
            Changes      = geminiResult.Changes is { Count: > 0 }
                            ? string.Join(", ", geminiResult.Changes)
                            : null,
            Resolved     = !geminiResult.HasChanges,
        };
        db.RoomCheckResults.Add(result);
        await db.SaveChangesAsync();

        if (geminiResult.HasChanges)
        {
            Console.WriteLine($"[RoomInfoCheckJob] 差分検出: {room.RoomName} → {result.Changes}");
            var reviewUrl = $"{baseUrl}/admin/room-check/{result.Id}?token={result.ReviewToken}";
            await NotifySlackAsync(slackBotToken, slackChannel, room, result.Changes, reviewUrl);
        }
        else
        {
            Console.WriteLine($"[RoomInfoCheckJob] 差分なし: {room.RoomName}");
        }
    }

    // ── Gemini呼び出し（リトライあり） ───────────────────
    private async Task<GeminiRoomCheckResponse?> CallGeminiWithRetryAsync(
        string apiKey, KenketsuRoom room, string url, List<RoomDismissedDiff> dismissedDiffs)
    {
        for (int attempt = 0; attempt < MaxRetry; attempt++)
        {
            try
            {
                return await CallGeminiAsync(apiKey, room, url, dismissedDiffs);
            }
            catch (GeminiRateLimitException)
            {
                Console.WriteLine($"[RoomInfoCheckJob] レート制限 → {RateLimitRetryDelay / 1000}秒待機 (attempt {attempt + 1})");
                await Task.Delay(RateLimitRetryDelay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoomInfoCheckJob] Geminiエラー: {ex.Message}");
                return null;
            }
        }
        return null;
    }

    private async Task<GeminiRoomCheckResponse?> CallGeminiAsync(
        string apiKey, KenketsuRoom room, string url, List<RoomDismissedDiff> dismissedDiffs)
    {
        var dbHours = room.BusinessHours
            .OrderBy(h => h.DayType)
            .Select(h => new
            {
                区分 = h.DayType == 0 ? "平日" : "土日祝",
                全血受付 = h.WholeReceptionStart.HasValue
                    ? $"{h.WholeReceptionStart:HH\\:mm}〜{h.WholeReceptionEnd:HH\\:mm}" : "なし",
                全血昼中断 = h.WholeLunchStart.HasValue
                    ? $"{h.WholeLunchStart:HH\\:mm}〜{h.WholeLunchEnd:HH\\:mm}" : "なし",
                成分受付 = h.CompReceptionStart.HasValue
                    ? $"{h.CompReceptionStart:HH\\:mm}〜{h.CompReceptionEnd:HH\\:mm}" : "なし",
                成分昼中断 = h.CompLunchStart.HasValue
                    ? $"{h.CompLunchStart:HH\\:mm}〜{h.CompLunchEnd:HH\\:mm}" : "なし",
            });

        var fieldLabels = new Dictionary<string, string>
        {
            ["city"]             = "市区町村",
            ["can_whole"]        = "全血献血",
            ["can_plasma"]       = "血漿成分献血",
            ["can_platelet"]     = "血小板成分献血",
            ["closed_days"]      = "定休日",
            ["business_hours_0"] = "営業時間（平日）",
            ["business_hours_1"] = "営業時間（土日祝）",
        };
        var dismissedSection = dismissedDiffs.Count > 0
            ? $"""

            【確認済みの差分（再度検出しないでください）】
            以下の項目はページとDBの値が異なりますが、管理者が確認済みのため差分として扱わないでください。
            has_changesの判定およびchangesへの記載から除外してください。
            {string.Join("\n", dismissedDiffs.Select(d =>
                $"- {(fieldLabels.TryGetValue(d.Field, out var label) ? label : d.Field)}: ページ上の値は {d.GeminiValue}"))}

            """
            : "";

        var prompt = $$"""
            以下のURLにある献血ルーム「{{room.RoomName}}」のJRC公式ページから、下記の項目をすべてページ上の記載通りに抽出してください。
            抽出できた値を使って、DB登録情報と比較し差分を報告してください。
            {{dismissedSection}}
            【DB登録情報（比較用）】
            - 市区町村: {{room.City ?? "未登録"}}
            - 全血献血: {{(room.CanWhole == true ? "可" : room.CanWhole == false ? "不可" : "未設定")}}
            - 血漿成分献血: {{(room.CanPlasma == true ? "可" : room.CanPlasma == false ? "不可" : "未設定")}}
            - 血小板成分献血: {{(room.CanPlatelet == true ? "可" : room.CanPlatelet == false ? "不可" : "未設定")}}
            - 定休日: {{room.ClosedDays ?? "なし"}}
            - 営業時間: {{JsonSerializer.Serialize(dbHours, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })}}

            【抽出・返答ルール】
            - city, can_whole, can_plasma, can_platelet, closed_days, business_hours はページから必ず抽出して返してください。ページに記載がない場合のみnullにしてください。
            - business_hours は平日(day_type:0)と土日祝(day_type:1)をそれぞれ抽出してください。
            - 時刻は "HH:mm" 形式（例: "09:00"）、昼中断なしの場合はnullにしてください。
            - has_changes はDB登録情報とページ上の抽出値を比較し、1つでも差異があればtrue、完全一致ならfalseにしてください。
            - changes にhas_changes:trueの場合は差分内容を具体的に列挙し、falseの場合は空配列にしてください。
            - has_changesがtrueの場合、changesは必ず1件以上含めてください。
            - JSON形式のみで返答し、他のテキストは一切含めないでください。

            {
              "city": "市区町村名",
              "can_whole": true または false,
              "can_plasma": true または false,
              "can_platelet": true または false,
              "closed_days": "定休日テキスト（なしの場合は「なし」）",
              "business_hours": [
                {
                  "day_type": 0,
                  "whole_reception_start": "09:00",
                  "whole_reception_end": "17:00",
                  "whole_lunch_start": "12:00",
                  "whole_lunch_end": "13:00",
                  "comp_reception_start": "09:00",
                  "comp_reception_end": "16:30",
                  "comp_lunch_start": "12:00",
                  "comp_lunch_end": "13:00"
                },
                {
                  "day_type": 1,
                  ...
                }
              ],
              "has_changes": true または false,
              "changes": ["差分の説明1", "差分の説明2"]
            }
            """;

        var requestBody = new
        {
            tools = new[] { new { url_context = new { } } },
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = $"URL: {url}\n\n{prompt}" }
                    }
                }
            }
        };

        using var http = new HttpClient();
        var response = await http.PostAsync(
            $"{GeminiApiUrl}?key={apiKey}",
            new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new GeminiRateLimitException();
            throw new Exception($"Gemini APIエラー ({(int)response.StatusCode}): {responseText}");
        }

        var responseJson = JsonDocument.Parse(responseText);
        var rawText = responseJson.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text").GetString() ?? "{}";

        // マークダウンコードブロック除去
        rawText = rawText.Trim();
        if (rawText.StartsWith("```")) rawText = string.Join("\n", rawText.Split('\n').Skip(1));
        if (rawText.EndsWith("```"))  rawText = rawText[..rawText.LastIndexOf("```")];
        rawText = rawText.Trim();

        var result = JsonSerializer.Deserialize<GeminiRoomCheckResponse>(rawText,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            });

        // changesが1件以上あるのにhas_changesがfalseの場合は補正
        if (result is not null && result.Changes is { Count: > 0 } && !result.HasChanges)
        {
            Console.WriteLine("[RoomInfoCheckJob] has_changesがfalseだがchangesが存在するため補正します");
            result.HasChanges = true;
        }

        return result;
    }

    // ── Slack通知 ─────────────────────────────────────────
    private static async Task NotifySlackAsync(
        string botToken, string channel, KenketsuRoom room, string? changes, string reviewUrl)
    {
        if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(channel)) return;

        var text = $":warning: *献血ルーム情報の差分を検出しました*\n" +
                   $"*ルーム名:* {room.RoomName}\n" +
                   $"*公式サイト:* {room.RoomUrl}\n" +
                   $"*差分内容:* {changes ?? "詳細不明"}\n" +
                   $"<{reviewUrl}|変更内容を確認・承認する>";

        var payload = new { channel, text, unfurl_links = false };

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {botToken}");
            await http.PostAsync(SlackApiUrl,
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RoomInfoCheckJob] Slack通知失敗: {ex.Message}");
        }
    }

    private sealed class GeminiRateLimitException : Exception { }

    public sealed class GeminiRoomCheckResponse
    {
        public string?                    City          { get; set; }
        public bool?                      CanWhole      { get; set; }
        public bool?                      CanPlasma     { get; set; }
        public bool?                      CanPlatelet   { get; set; }
        public string?                    ClosedDays    { get; set; }
        public List<GeminiBusinessHours>? BusinessHours { get; set; }
        public bool                       HasChanges    { get; set; }
        public List<string>?              Changes       { get; set; }
    }

    public sealed class GeminiBusinessHours
    {
        // 0=平日, 1=土日祝
        public int     DayType              { get; set; }
        public string? WholeReceptionStart  { get; set; }
        public string? WholeReceptionEnd    { get; set; }
        public string? WholeLunchStart      { get; set; }
        public string? WholeLunchEnd        { get; set; }
        public string? CompReceptionStart   { get; set; }
        public string? CompReceptionEnd     { get; set; }
        public string? CompLunchStart       { get; set; }
        public string? CompLunchEnd         { get; set; }
    }
}
