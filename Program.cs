using KenketsuNote.Infrastructure;
using KenketsuNote;
using KenketsuNote.Data;

string connectionString = Environment.GetEnvironmentVariable("KENKETSUNOTE_CONNECTION_STRING") ?? "";
Console.WriteLine($"KENKETSUNOTE_CONNECTION_STRING:{connectionString?.Length ?? 0}");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All);
});
builder.Services.AddDbContext<KenketsuNoteContext>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(6002);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

MasterData.Load();

app.Run();
