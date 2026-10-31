using System.Globalization;
using System.Text.Json;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Infrastructure.Http;

namespace SchnapperRadar.Infrastructure.Sources.Vinted;

/// <summary>Vinted-JSON → Listing. Bewusst mit JsonDocument statt fester DTOs, weil sich die inoffizielle API gerne ändert.</summary>
internal static class VintedMapper
{
    public static readonly IReadOnlyDictionary<ItemCondition, int> StatusIds = new Dictionary<ItemCondition, int>
    {
        [ItemCondition.NewWithTags] = 6,
        [ItemCondition.New] = 1,
        [ItemCondition.VeryGood] = 2,
        [ItemCondition.Good] = 3,
        [ItemCondition.Acceptable] = 4,
    };

    public static List<Listing> ParseCatalog(string json, string host)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return [];

        var result = new List<Listing>();
        foreach (var item in items.EnumerateArray())
        {
            var id = Str(item, "id");
            var title = Str(item, "title");
            if (id is null || title is null) continue;

            var photo = item.TryGetProperty("photo", out var ph) && ph.ValueKind == JsonValueKind.Object ? ph : (JsonElement?)null;
            var image = photo is { } p ? Str(p, "url") : null;
            long? ts = photo is { } p2 && p2.TryGetProperty("high_resolution", out var hr) && hr.ValueKind == JsonValueKind.Object &&
                       hr.TryGetProperty("timestamp", out var tse) && tse.TryGetInt64(out var tsv) ? tsv : null;

            var price = Money(item, "price");
            var total = Money(item, "total_item_price");
            var fee = Money(item, "service_fee") ?? (total is not null && price is not null && total > price ? total - price : null);

            var user = item.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object ? u : (JsonElement?)null;

            result.Add(new Listing
            {
                Platform = Platform.Vinted,
                ExternalId = id,
                Url = SafeUrl(Str(item, "url"), host) ?? $"https://{host}/items/{id}",
                Title = title.Trim(),
                Price = price,
                PlatformBuyerFee = fee,
                Brand = Str(item, "brand_title"),
                Size = Str(item, "size_title"),
                Condition = GermanText.ParseCondition(Str(item, "status")),
                ImageUrls = image is not null && image.StartsWith("https://", StringComparison.Ordinal) ? [image] : [],
                ImageCount = image is null ? 0 : 1,
                ShippingAvailable = true,
                PickupAvailable = false,
                PostedAt = ts is { } t ? DateTimeOffset.FromUnixTimeSeconds(t) : null,
                Seller = new SellerInfo
                {
                    ExternalId = user is { } us ? Str(us, "id") : null,
                    Name = user is { } us2 ? Str(us2, "login") : null,
                    IsCommercial = user is { } us3 && us3.TryGetProperty("business", out var b) && b.ValueKind == JsonValueKind.True,
                },
            });
        }

        return result;
    }

    public static void ApplyUser(Listing listing, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var user = doc.RootElement.TryGetProperty("user", out var u) ? u : doc.RootElement;

        var reputation = Num(user, "feedback_reputation");
        var s = listing.Seller;
        s.RatingCount = (int?)Num(user, "feedback_count");
        // Vinted liefert 0..1 (manchmal 0..5) – auf Prozent normalisieren.
        s.RatingPercent = reputation switch
        {
            null => null,
            <= 1 => Math.Round(reputation.Value * 100, 1),
            <= 5 => Math.Round(reputation.Value / 5 * 100, 1),
            _ => null,
        };
        s.SoldItems = (int?)Num(user, "given_item_count");
        s.ActiveListings = (int?)Num(user, "item_count");
        s.Name = Str(user, "login") ?? s.Name;
        s.IsCommercial = user.TryGetProperty("business", out var b) && b.ValueKind == JsonValueKind.True;
        if (Str(user, "created_at") is { } created && DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.None, out var c))
            s.MemberSince = DateOnly.FromDateTime(c.UtcDateTime);
        s.Badges = s.RatingCount is > 0 && s.RatingPercent is not null
            ? [$"{s.RatingPercent:0.#} % bei {s.RatingCount} Bewertungen"]
            : ["Keine Bewertungen"];
        s.IsLoaded = true;
    }

    public static void ApplyItemDetails(Listing listing, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement.TryGetProperty("item", out var i) ? i : doc.RootElement;

        if (Str(item, "description") is { Length: > 0 } d) listing.Description = d.Trim();
        if (Num(item, "package_size_id") is { } size) listing.PackageSize = (int)size;
        if (item.TryGetProperty("photos", out var photos) && photos.ValueKind == JsonValueKind.Array)
        {
            var urls = photos.EnumerateArray().Select(p => Str(p, "url")).Where(u => u?.StartsWith("https://", StringComparison.Ordinal) == true)
                .Select(u => u!).ToList();
            if (urls.Count > 0)
            {
                listing.ImageUrls = urls.Take(8).ToList();
                listing.ImageCount = urls.Count;
            }
        }
        if (Str(item, "status") is { } status) listing.Condition = GermanText.ParseCondition(status);
    }

    private static string? SafeUrl(string? url, string host) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == host ? u.ToString() : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;

    private static double? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        } : null;

    /// <summary>Preis kommt je nach API-Version als "12.0" oder {"amount":"12.0","currency_code":"EUR"}.</summary>
    private static decimal? Money(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        var raw = v.ValueKind switch
        {
            JsonValueKind.Object when v.TryGetProperty("amount", out var a) => a.ValueKind == JsonValueKind.Number ? a.GetRawText() : a.GetString(),
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
