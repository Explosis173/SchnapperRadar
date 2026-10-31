using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SchnapperRadar.Api.Security;
using SchnapperRadar.Application.Dtos;
using SchnapperRadar.Application.Services;

namespace SchnapperRadar.Api.Endpoints;

public static class ApiEndpoints
{
    public const string LoginRateLimit = "login";
    public const string SearchRateLimit = "search";

    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // ---------- Auth ----------
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", async (LoginRequest req, PasswordVerifier verifier, HttpContext ctx) =>
        {
            if (!verifier.Verify(req.Password))
            {
                await Task.Delay(Random.Shared.Next(300, 700)); // bremst Brute-Force zusätzlich
                return Results.Unauthorized();
            }
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "owner")], CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
            return Results.NoContent();
        }).RequireRateLimiting(LoginRateLimit).AllowAnonymous();

        auth.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).AllowAnonymous();

        auth.MapGet("/me", (HttpContext ctx) =>
            ctx.User.Identity?.IsAuthenticated == true ? Results.Ok(new { authenticated = true }) : Results.Unauthorized()).AllowAnonymous();

        // ---------- Feed ----------
        api.MapGet("/feed", async (int? offset, int? limit, FeedService feed, CancellationToken ct) =>
            Results.Ok(await feed.GetPageAsync(offset ?? 0, limit ?? 20, ct)));

        api.MapPost("/feed/refresh", (FeedRefreshTrigger trigger) =>
        {
            trigger.Request();
            return Results.Accepted();
        }).RequireRateLimiting(SearchRateLimit);

        api.MapGet("/saved", async (FeedService feed, CancellationToken ct) => Results.Ok(await feed.GetSavedAsync(ct)));

        api.MapPost("/listings/{id:guid}/hide", async (Guid id, FeedService feed, CancellationToken ct) =>
            (await feed.HideAsync(id, ct)) is { IsSuccess: true } ? Results.NoContent() : Results.NotFound());

        api.MapPost("/listings/{id:guid}/save", async (Guid id, FeedService feed, CancellationToken ct) =>
            (await feed.ToggleSavedAsync(id, ct)) is { IsSuccess: true } r ? Results.Ok(new { saved = r.Value }) : Results.NotFound());

        // ---------- Suche ----------
        api.MapPost("/search", async (SearchRequest req, SearchService search, CancellationToken ct) =>
        {
            var result = await search.SearchAsync(req.ToCriteria(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
        }).RequireRateLimiting(SearchRateLimit);

        // ---------- Interessen ----------
        api.MapGet("/interests", async (InterestService s, CancellationToken ct) => Results.Ok(await s.GetAllAsync(ct)));

        api.MapPost("/interests", async (CreateInterestRequest req, InterestService s, CancellationToken ct) =>
            (await s.CreateAsync(req, ct)) is { IsSuccess: true } r ? Results.Ok(r.Value) : Results.BadRequest(new { error = "Ungültiges Interesse." }));

        api.MapPost("/interests/{id:guid}/toggle", async (Guid id, InterestService s, CancellationToken ct) =>
            (await s.ToggleAsync(id, ct)) is { IsSuccess: true } r ? Results.Ok(r.Value) : Results.NotFound());

        api.MapDelete("/interests/{id:guid}", async (Guid id, InterestService s, CancellationToken ct) =>
            (await s.DeleteAsync(id, ct)).IsSuccess ? Results.NoContent() : Results.NotFound());

        // ---------- System ----------
        api.MapGet("/system/status", async (SystemService s, CancellationToken ct) => Results.Ok(await s.GetStatusAsync(ct)));

        app.MapGet("/healthz", () => Results.Ok("ok")).AllowAnonymous();
    }
}
