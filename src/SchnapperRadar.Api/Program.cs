using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using SchnapperRadar.Api.Endpoints;
using SchnapperRadar.Api.Security;
using SchnapperRadar.Application;
using SchnapperRadar.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------- Sicherheit: ohne Passwort startet die App nur im Development-Modus ----------
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
var authOptions = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
if (!builder.Environment.IsDevelopment() && (authOptions.Password?.Length ?? 0) < AuthOptions.MinPasswordLength)
    throw new InvalidOperationException($"Auth__Password muss gesetzt sein (mind. {AuthOptions.MinPasswordLength} Zeichen).");

builder.Services.AddSingleton<PasswordVerifier>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.MaxDepth = 16;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "sr_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = authOptions.CookieSecure.Equals("SameAsRequest", StringComparison.OrdinalIgnoreCase)
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(authOptions.SessionDays);
        o.SlidingExpiration = true;
        // API: 401 statt Redirect auf eine Login-Seite.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

// Data-Protection-Keys persistieren, sonst sind Sessions nach jedem Neustart weg.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).SetApplicationName("SchnapperRadar");

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(ApiEndpoints.LoginRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    // Suche = Scraping + KI-Kosten → begrenzen.
    o.AddPolicy(ApiEndpoints.SearchRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        "search",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Hinter Azure Container Apps / Reverse-Proxy. Nur aktivieren, wenn wirklich ein Proxy davor sitzt.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("BehindProxy")) app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseCsrfGuard();

app.MapApi();
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();
