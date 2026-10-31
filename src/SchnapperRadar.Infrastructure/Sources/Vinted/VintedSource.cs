using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Common;
using SchnapperRadar.Application.Search;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Infrastructure.Http;

namespace SchnapperRadar.Infrastructure.Sources.Vinted;

/// <summary>Teilt Cookies zwischen den Handler-Instanzen der HttpClientFactory.</summary>
public sealed class VintedCookieJar
{
    public CookieContainer Container { get; } = new();
    public DateTimeOffset SessionCreatedAt { get; set; } = DateTimeOffset.MinValue;
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

/// <summary>Nutzt die (inoffizielle) Web-API, die auch die Vinted-Website verwendet.</summary>
internal sealed class VintedSource(
    IHttpClientFactory httpFactory,
    HostThrottle throttle,
    VintedCookieJar cookies,
    IOptions<ScraperOptions> options,
    ILogger<VintedSource> logger) : IListingSource
{
    public const string HttpClientName = "vinted";
    private const int PerPage = 96;
    private const int MaxPages = 3;
    private const string ThrottleKey = "vinted";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    public Platform Platform => Platform.Vinted;
    private string Host => options.Value.VintedHost;

    public async Task<Result<IReadOnlyList<Listing>>> SearchAsync(SourceQuery query, CancellationToken ct)
    {
        if (!options.Value.VintedEnabled) return Result<IReadOnlyList<Listing>>.Success([]);

        var all = new List<Listing>();
        for (var page = 1; page <= MaxPages && all.Count < query.MaxResults; page++)
        {
            var json = await GetJsonAsync(BuildCatalogUrl(query, page), ct);
            if (json is null) return all.Count > 0
                ? Result<IReadOnlyList<Listing>>.Success(all)
                : Result<IReadOnlyList<Listing>>.Failure("Suche fehlgeschlagen (Bot-Schutz oder offline?).");

            var items = VintedMapper.ParseCatalog(json, Host);
            all.AddRange(items);
            if (items.Count < PerPage) break;
        }

        var result = all.DistinctBy(l => l.ExternalId).Take(query.MaxResults);
        if (query.OnlyPrivateSellers) result = result.Where(l => !l.Seller.IsCommercial);
        return Result<IReadOnlyList<Listing>>.Success(result.ToList());
    }

    public async Task<Result> EnrichAsync(Listing listing, CancellationToken ct)
    {
        if (!long.TryParse(listing.ExternalId, out var itemId)) return Result.Failure("Ungültige Vinted-ID.");

        // Item-Details sind "nice to have" (Beschreibung, Fotos, Paketgröße) – manchmal nur eingeloggt verfügbar.
        var itemJson = await GetJsonAsync($"https://{Host}/api/v2/items/{itemId}?localize=false", ct);
        if (itemJson is not null) TryApply(() => VintedMapper.ApplyItemDetails(listing, itemJson), listing);

        // Verkäuferprofil ist Pflicht – ohne Bewertungen kein Vertrauen.
        if (!long.TryParse(listing.Seller.ExternalId, out var userId)) return Result.Failure("Verkäufer-ID fehlt.");
        var userJson = await GetJsonAsync($"https://{Host}/api/v2/users/{userId}", ct);
        if (userJson is null) return Result.Failure("Verkäuferprofil nicht ladbar.");
        return TryApply(() => VintedMapper.ApplyUser(listing, userJson), listing)
            ? Result.Success()
            : Result.Failure("Verkäuferprofil unlesbar.");
    }

    internal string BuildCatalogUrl(SourceQuery q, int page)
    {
        var sb = new StringBuilder($"https://{Host}/api/v2/catalog/items?page={page}&per_page={PerPage}&order=newest_first&currency=EUR");
        sb.Append("&search_text=").Append(Uri.EscapeDataString(q.Query));
        if (q.MinPrice is { } min) sb.Append("&price_from=").Append(min.ToString("0.##", CultureInfo.InvariantCulture));
        if (q.MaxPrice is { } max) sb.Append("&price_to=").Append(max.ToString("0.##", CultureInfo.InvariantCulture));
        foreach (var c in q.Conditions)
            if (VintedMapper.StatusIds.TryGetValue(c, out var id)) sb.Append("&status_ids[]=").Append(id);
        return sb.ToString();
    }

    private bool TryApply(Action apply, Listing listing)
    {
        try
        {
            apply();
            return true;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Vinted-JSON für {Id} unlesbar", listing.ExternalId);
            return false;
        }
    }

    private async Task<string?> GetJsonAsync(string url, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await EnsureSessionAsync(force: attempt > 0, ct);
            await throttle.WaitAsync(ThrottleKey, TimeSpan.FromMilliseconds(options.Value.VintedMinDelayMs), ct);
            try
            {
                using var client = httpFactory.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.ParseAdd("application/json");
                using var response = await client.SendAsync(request, ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && attempt == 0) continue;
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Vinted {Status} für {Url}", (int)response.StatusCode, url);
                    return null;
                }
                return await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Vinted-Request fehlgeschlagen: {Url}", url);
                return null;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Vinted-Timeout: {Url}", url);
                return null;
            }
        }
        return null;
    }

    /// <summary>Vinted verlangt ein Session-Cookie, das man beim Aufruf der Startseite bekommt.</summary>
    private async Task EnsureSessionAsync(bool force, CancellationToken ct)
    {
        if (!force && DateTimeOffset.UtcNow - cookies.SessionCreatedAt < SessionLifetime) return;

        await cookies.Gate.WaitAsync(ct);
        try
        {
            if (!force && DateTimeOffset.UtcNow - cookies.SessionCreatedAt < SessionLifetime) return;
            await throttle.WaitAsync(ThrottleKey, TimeSpan.FromMilliseconds(options.Value.VintedMinDelayMs), ct);
            using var client = httpFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync($"https://{Host}/", ct);
            cookies.SessionCreatedAt = DateTimeOffset.UtcNow;
            if (!response.IsSuccessStatusCode) logger.LogWarning("Vinted-Session: Startseite lieferte {Status}", (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Vinted-Session konnte nicht aufgebaut werden");
        }
        finally
        {
            cookies.Gate.Release();
        }
    }
}
