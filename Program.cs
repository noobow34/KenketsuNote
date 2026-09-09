using Auth0.AspNetCore.Authentication;
using KenketsuNote.Data;
using KenketsuNote.Infrastructure;
using KenketsuNote.Jobs;
using KenketsuNote.Middleware;
using KenketsuNote.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Quartz;

string connectionString = Environment.GetEnvironmentVariable("KENKETSUNOTE_CONNECTION_STRING") ?? "";
string auth0Domain      = Environment.GetEnvironmentVariable("AUTH0_DOMAIN")                    ?? "";
string auth0ClientId    = Environment.GetEnvironmentVariable("AUTH0_CLIENT_ID")                  ?? "";
foreach (var (key, val) in new[]
{
    ("KENKETSUNOTE_CONNECTION_STRING", connectionString),
    ("AUTH0_DOMAIN",                   auth0Domain),
    ("AUTH0_CLIENT_ID",                auth0ClientId),
    ("ADMIN_KEY",                      Environment.GetEnvironmentVariable("ADMIN_KEY")                      ?? ""),
    ("ADMIN_VALUE",                    Environment.GetEnvironmentVariable("ADMIN_VALUE")                    ?? ""),
    ("GEMINI_API_KEY",                 Environment.GetEnvironmentVariable("GEMINI_API_KEY")                 ?? ""),
    ("SLACK_BOT_TOKEN",                Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN")                ?? ""),
    ("SLACK_ROOM_CHECK_CHANNEL",       Environment.GetEnvironmentVariable("SLACK_ROOM_CHECK_CHANNEL")       ?? ""),
    ("KENKETSUNOTE_BASE_URL",          Environment.GetEnvironmentVariable("KENKETSUNOTE_BASE_URL")          ?? ""),
})
    Console.WriteLine($"{key}:{(string.IsNullOrEmpty(val) ? "NotSet" : "Loaded")}");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All);
});
builder.Services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain   = auth0Domain;
    options.ClientId = auth0ClientId;
});
builder.Services.AddDbContext<KenketsuNoteContext>();

// デプロイスクリプトのヘルスチェック用。判定対象は「プロセスが起動してリクエストを受けられるか」と
// 「DBに到達できるか」のみ。ラブラッドのスクレイピングやQuartzジョブの実行状況は、
// 外部要因の失敗でデプロイがロールバックされるのを避けるため意図的に含めない。
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres");

builder.Services.AddQuartz(q =>
{
    // トリガー（実行スケジュール）はDBの job_schedule から起動時に登録する
    foreach (var def in JobRegistry.Jobs)
        q.AddJob(def.JobType, new JobKey(def.Name), opts => opts.StoreDurably());
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
});
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<ConditionalAuthRedirectMiddleware>();
app.UseAuthorization();
app.UseMiddleware<AccessLogMiddleware>();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// ローカルからの curl で無認証で叩ける必要があるため AllowAnonymous。
// ConditionalAuthRedirectMiddleware 側の ExcludeList にも /HEALTHZ を追加してある。
app.MapHealthChecks("/healthz").AllowAnonymous();

MasterData.Load();

// QuartzジョブからDIコンテナを参照できるようスケジューラコンテキストに登録
var scheduler = await app.Services.GetRequiredService<Quartz.ISchedulerFactory>().GetScheduler();
scheduler.Context["services"] = app.Services;

// DBのジョブ設定（有効/無効・cron式）を読んでトリガーを登録
using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<KenketsuNoteContext>();

    // ルーム情報チェックジョブの状態行（次回offset・ログ保持日数など）を確保
    if (await db.RoomCheckJobStates.FindAsync(1) is null)
    {
        db.RoomCheckJobStates.Add(new RoomCheckJobState { Id = 1, NextOffset = 0 });
        await db.SaveChangesAsync();
    }

    foreach (var job in await JobScheduleService.SyncAllAsync(db, scheduler))
    {
        var schedule = job.IsEnabled
            ? $"{job.CronExpression} (JST) 次回 {JobScheduleService.NextRunJst(job.CronExpression):yyyy-MM-dd HH:mm}"
            : "無効";
        Console.WriteLine($"[Quartz] {job.JobName} スケジュール: {schedule}");
    }
}

app.Run();
