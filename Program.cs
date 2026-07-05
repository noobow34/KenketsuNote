using Auth0.AspNetCore.Authentication;
using KenketsuNote.Middleware;
using KenketsuNote.Infrastructure;
using KenketsuNote.Data;
using KenketsuNote.Jobs;
using Quartz;

string connectionString = Environment.GetEnvironmentVariable("KENKETSUNOTE_CONNECTION_STRING") ?? "";
Console.WriteLine($"KENKETSUNOTE_CONNECTION_STRING:{connectionString?.Length ?? 0}");
string auth0Domain   = Environment.GetEnvironmentVariable("AUTH0_DOMAIN")    ?? "";
string auth0ClientId = Environment.GetEnvironmentVariable("AUTH0_CLIENT_ID")  ?? "";
Console.WriteLine($"AUTH0_DOMAIN:{auth0Domain.Length}");
Console.WriteLine($"AUTH0_CLIENT_ID:{auth0ClientId.Length}");

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
    // ジョブ定義
    var jobKey = new JobKey("RoomInfoCheckJob");
    q.AddJob<RoomInfoCheckJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("RoomInfoCheckJob-trigger")
        .WithCronSchedule("0 30 06 * * ?"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<ConditionalAuthRedirectMiddleware>();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

MasterData.Load();

// QuartzジョブからDIコンテナを参照できるようスケジューラコンテキストに登録
var scheduler = await app.Services.GetRequiredService<Quartz.ISchedulerFactory>().GetScheduler();
scheduler.Context["services"] = app.Services;

app.Run();
