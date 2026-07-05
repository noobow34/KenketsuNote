using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace KenketsuNote.Classes;

public class ConditionalAuthRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly string[] ExcludeList = [".CSS", ".JS", ".PNG", ".JPG", ".JPEG", ".GIF", ".ICO", ".WEBP", ".WOFF", ".WOFF2", "/ACCOUNT/LOGIN", "/SETCOOKIE"];
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
            string returnUrl = context.Request.Path;
            context.Response.Cookies.Append(AdminKey, adminCookieValue, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
            var authenticationProperties = new LoginAuthenticationPropertiesBuilder()
                .WithRedirectUri(returnUrl)
                .Build();
            await context.ChallengeAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
            return;
        }

        await _next(context);
    }
}
