namespace SchnapperRadar.Api.Security;

public static class SecurityHeaders
{
    /// <summary>Bilder nur von den Plattform-CDNs, Skripte nur von uns selbst.</summary>
    private const string Csp =
        "default-src 'self'; " +
        "img-src 'self' data: https://img.kleinanzeigen.de https://*.kleinanzeigen.de https://*.vinted.net https://*.vinted.com; " +
        "script-src 'self'; style-src 'self'; connect-src 'self'; font-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'none'; form-action 'self'; object-src 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h["Content-Security-Policy"] = Csp;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            if (ctx.Request.Path.StartsWithSegments("/api")) h["Cache-Control"] = "no-store";
            await next();
        });
}

/// <summary>
/// CSRF-Schutz: Browser können fremd-seitig keine Custom-Header setzen, ohne dass CORS (das wir nicht erlauben) zustimmt.
/// Zusätzlich SameSite=Strict am Cookie.
/// </summary>
public static class CsrfGuard
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "SchnapperRadar";

    public static IApplicationBuilder UseCsrfGuard(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var method = ctx.Request.Method;
            var isStateChanging = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
            if (isStateChanging && ctx.Request.Path.StartsWithSegments("/api") &&
                ctx.Request.Headers[HeaderName] != HeaderValue)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next();
        });
}
