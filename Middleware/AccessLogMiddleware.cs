using System.Text.RegularExpressions;
using KenketsuNote.Data;

namespace KenketsuNote.Middleware;

public class AccessLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;

    // 記録対象パスのパターン（正規化後の表示名）
    private static readonly (Regex Pattern, string Label)[] PagePatterns =
    [
        (new Regex(@"^/u/[^/]+/stamp$",   RegexOptions.IgnoreCase), "/u/{userId}/stamp"),
        (new Regex(@"^/u/[^/]+/tracker$", RegexOptions.IgnoreCase), "/u/{userId}/tracker"),
        (new Regex(@"^/u/[^/]+$",         RegexOptions.IgnoreCase), "/u/{userId}"),
        (new Regex(@"^/rooms$",           RegexOptions.IgnoreCase), "/rooms"),
        (new Regex(@"^/manual$",          RegexOptions.IgnoreCase), "/manual"),
        (new Regex(@"^/$",                RegexOptions.IgnoreCase), "/"),
    ];

    public AccessLogMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _scopeFactory = scopeFactory;
    }

    public async Task Invoke(HttpContext context)
    {
        await _next(context);

        if (context.Request.Method != HttpMethods.Get) return;

        var path = context.Request.Path.Value ?? "";
        string? label = null;
        foreach (var (pattern, l) in PagePatterns)
        {
            if (pattern.IsMatch(path)) { label = l; break; }
        }
        if (label is null) return;

        var isAdmin = context.User.Identity?.IsAuthenticated ?? false;
        var ip      = context.Connection.RemoteIpAddress?.ToString();

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KenketsuNoteContext>();
                db.AccessLogs.Add(new AccessLog
                {
                    AccessedAt = DateTimeOffset.UtcNow,
                    Page       = label,
                    IsAdmin    = isAdmin,
                    IpAddress  = ip,
                });
                await db.SaveChangesAsync();
            }
            catch { /* ログ失敗は握りつぶす */ }
        });
    }
}
