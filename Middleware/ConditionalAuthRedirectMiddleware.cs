using KenketsuNote.Auth;

namespace KenketsuNote.Middleware;

/// <summary>
/// 管理者の端末だけを自動でログインへ誘導する。
/// サイトは全ページがログイン不要のため、Cloudflare Accessの保護対象は
/// /Account/Login に絞ってある。ここでそこへリダイレクトすることで、
/// 目印のCookieを持つ端末にだけAccessのログイン画面を出す。
/// </summary>
public class ConditionalAuthRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly string[] ExcludeList = [".CSS", ".JS", ".PNG", ".JPG", ".JPEG", ".GIF", ".ICO", ".WEBP", ".WOFF", ".WOFF2", "/ACCOUNT/LOGIN", "/ACCOUNT/LOGOUT", "/SETCOOKIE", "/HEALTHZ"];
    private static readonly string AdminKey   = Environment.GetEnvironmentVariable("ADMIN_KEY")   ?? "";
    private static readonly string AdminValue = Environment.GetEnvironmentVariable("ADMIN_VALUE") ?? "";

    public ConditionalAuthRedirectMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        bool autoLoginTarget = !ExcludeList.Any(s => context.Request.Path.Value!.Contains(s, StringComparison.OrdinalIgnoreCase));

        if (context.User.Identity!.IsAuthenticated || !autoLoginTarget)
        {
            await _next(context);
            return;
        }

        context.Request.Cookies.TryGetValue(AdminKey, out string? adminCookieValue);
        if (!string.IsNullOrEmpty(AdminKey) && adminCookieValue == AdminValue)
        {
            string returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Cookies.Append(AdminKey, adminCookieValue, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
            context.Response.Redirect(CloudflareAccess.BuildLoginUrl(returnUrl));
            return;
        }

        await _next(context);
    }
}
