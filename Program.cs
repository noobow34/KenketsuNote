using Auth0.AspNetCore.Authentication;
using KenketsuNote.Data;
using KenketsuNote.Infrastructure;
using KenketsuNote.Jobs;
using KenketsuNote.Middleware;
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
    Console.WriteLine($"{key}:{val.Length}");

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

builder.Services.AddQuartz(q =>
{
    var roomCheckJobKey = new JobKey("RoomInfoCheckJob");
    q.AddJob<RoomInfoCheckJob>(opts => opts.WithIdentity(roomCheckJobKey).StoreDurably());

    var cleanupJobKey = new JobKey("LogCleanupJob");
    q.AddJob<LogCleanupJob>(opts => opts.WithIdentity(cleanupJobKey).StoreDurably());
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

MasterData.Load();

// QuartzジョブからDIコンテナを参照できるようスケジューラコンテキストに登録
var scheduler = await app.Services.GetRequiredService<Quartz.ISchedulerFactory>().GetScheduler();
scheduler.Context["services"] = app.Services;

// DBから実行時刻を読んでトリガーを登録
using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<KenketsuNoteContext>();
    var state = await db.RoomCheckJobStates.FindAsync(1);
    if (state is null)
    {
        state = new RoomCheckJobState { Id = 1, NextOffset = 0 };
        db.RoomCheckJobStates.Add(state);
        await db.SaveChangesAsync();
    }
    var cron = $"0 {state.ScheduledMinute} {state.ScheduledHour} * * ?";
    Console.WriteLine($"[Quartz] RoomInfoCheckJob スケジュール: {cron} (JST)");
    var trigger = TriggerBuilder.Create()
        .WithIdentity("RoomInfoCheckJob-trigger")
        .ForJob(new JobKey("RoomInfoCheckJob"))
        .WithCronSchedule(cron)
        .Build();
    await scheduler.ScheduleJob(trigger);

    // ログ削除ジョブ：毎日 3:00 (JST) に実行
    var cleanupTrigger = TriggerBuilder.Create()
        .WithIdentity("LogCleanupJob-trigger")
        .ForJob(new JobKey("LogCleanupJob"))
        .WithCronSchedule("0 0 3 * * ?")
        .Build();
    await scheduler.ScheduleJob(cleanupTrigger);
    Console.WriteLine("[Quartz] LogCleanupJob スケジュール: 毎日 3:00 (JST)");
}

app.Run();
