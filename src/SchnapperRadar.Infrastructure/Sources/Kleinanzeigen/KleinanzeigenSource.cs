using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Common;
using SchnapperRadar.Application.Search;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Infrastructure.Http;

namespace SchnapperRadar.Infrastructure.Sources.Kleinanzeigen;

internal sealed class KleinanzeigenSource(
    IHttpClientFactory httpFactory,
    HostThrottle throttle,
    IOptions<ScraperOptions> options,
    ILogger<KleinanzeigenSource> logger) : IListingSource
{
    public const string HttpClientName = "kleinanzeigen";
    private const int MaxPages = 5;
    private const string ThrottleKey = "kleinanzeigen";

    public Platform Platform => Platform.Kleinanzeigen;

    public async Task<Result<IReadOnlyList<Listing>>> SearchAsync(SourceQuery query, CancellationToken ct)
    {
        if (!options.Value.KleinanzeigenEnabled) return Result<IReadOnlyList<Listing>>.Success([]);

        var all = new List<Listing>();
        for (var page = 1; page <= MaxPages && all.Count < query.MaxResults; page++)
        {
            var html = await GetAsync(BuildSearchUrl(query, page), ct);
            if (html is null) return all.Count > 0
                ? Result<IReadOnlyList<Listing>>.Success(all)
                : Result<IReadOnlyList<Listing>>.Failure("Suche fehlgeschlagen (blockiert oder offline?).");

            var items = KleinanzeigenParser.ParseSearchResults(html);
            if (items.Count == 0)
            {
                if (page == 1) logger.LogInformation("Kleinanzeigen: keine Treffer/Parser fand nichts für '{Query}'", query.Query);
                break;
            }
            all.AddRange(items);
        }

        return Result<IReadOnlyList<Listing>>.Success(all.DistinctBy(l => l.ExternalId).Take(query.MaxResults).ToList());
    }

    public async Task<Result> EnrichAsync(Listing listing, CancellationToken ct)
    {
        if (!Uri.TryCreate(listing.Url, UriKind.Absolute, out var uri) || uri.Host != "www.kleinanzeigen.de")
            return Result.Failure("Ungültige Kleinanzeigen-URL.");

        var html = await GetAsync(uri.ToString(), ct);
        if (html is null) return Result.Failure("Detailseite nicht ladbar.");
        KleinanzeigenParser.ApplyDetails(listing, html);
        return Result.Success();
    }

    internal static string BuildSearchUrl(SourceQuery q, int page)
    {
        var p = new Dictionary<string, string?>
        {
            ["keywords"] = q.Query,
            ["categoryId"] = "",
            ["locationStr"] = q.NearPostalCode ?? "",
            ["locationId"] = "",
            ["radius"] = q.RadiusKm?.ToString(CultureInfo.InvariantCulture) ?? "0",
            ["sortingField"] = "SORTING_DATE",
            ["adType"] = "OFFER",
            ["posterType"] = q.OnlyPrivateSellers ? "PRIVATE" : "",
            ["pageNum"] = page.ToString(CultureInfo.InvariantCulture),
            ["action"] = "find",
            ["maxPrice"] = q.MaxPrice?.ToString("0", CultureInfo.InvariantCulture) ?? "",
            ["minPrice"] = q.MinPrice?.ToString("0", CultureInfo.InvariantCulture) ?? "",
        };
        var qs = string.Join('&', p.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));
        return $"{KleinanzeigenParser.BaseUrl}/s-suchanfrage.html?{qs}";
    }

    private async Task<string?> GetAsync(string url, CancellationToken ct)
    {
        await throttle.WaitAsync(ThrottleKey, TimeSpan.FromMilliseconds(options.Value.KleinanzeigenMinDelayMs), ct);
        try
        {
            using var client = httpFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Kleinanzeigen {Status} für {Url}", (int)response.StatusCode, url);
                return null;
            }
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Kleinanzeigen-Request fehlgeschlagen: {Url}", url);
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Kleinanzeigen-Timeout: {Url}", url);
            return null;
        }
    }
}
