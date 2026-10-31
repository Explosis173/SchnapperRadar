using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Infrastructure.Http;

namespace SchnapperRadar.Infrastructure.Sources.Kleinanzeigen;

/// <summary>
/// HTML → Listing. Alle Selektoren an einer Stelle: wenn Kleinanzeigen das Markup ändert, hier anpassen.
/// Wo möglich wird mit Text-Regex statt fragiler CSS-Pfade gearbeitet.
/// </summary>
internal static partial class KleinanzeigenParser
{
    public const string BaseUrl = "https://www.kleinanzeigen.de";

    private static class Sel
    {
        public const string ResultItem = "article.aditem";
        public const string Title = "h2 a, .text-module-begin a, a.ellipsis";
        public const string Price = ".aditem-main--middle--price-shipping--price, .aditem-main--middle--price";
        public const string Location = ".aditem-main--top--left";
        public const string Date = ".aditem-main--top--right";
        public const string Snippet = ".aditem-main--middle--description";
        public const string Tags = ".simpletag, .aditem-main--middle--price-shipping--shipping, .aditem-main--bottom";
        public const string Image = ".aditem-image img, .imagebox img";

        public const string DetailTitle = "#viewad-title";
        public const string DetailPrice = "#viewad-price";
        public const string DetailLocality = "#viewad-locality";
        public const string DetailDescription = "#viewad-description-text";
        public const string DetailAttributes = ".addetailslist--detail";
        public const string DetailAttributeValue = ".addetailslist--detail--value";
        public const string DetailImages = "#viewad-image, .galleryimage-element img, .galleryimage--element img";
        public const string DetailDate = "#viewad-extra-info";
        public const string SellerBox = "#viewad-contact, #viewad-profile-box, .userprofile";
        public const string SellerName = ".userprofile-vip a, .userprofile-vip";
        public const string SellerLink = "a[href*='userId='], a[href*='/s-bestandsliste']";
    }

    private static readonly HtmlParser Parser = new();

    public static List<Listing> ParseSearchResults(string html)
    {
        var doc = Parser.ParseDocument(html);
        var result = new List<Listing>();

        foreach (var item in doc.QuerySelectorAll(Sel.ResultItem))
        {
            var id = item.GetAttribute("data-adid");
            var titleEl = item.QuerySelector(Sel.Title);
            var href = item.GetAttribute("data-href") ?? titleEl?.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(id) || titleEl is null || string.IsNullOrWhiteSpace(href)) continue;
            var url = AbsoluteUrl(href);
            if (!IsOwnUrl(url)) continue;

            var (price, negotiable) = GermanText.ParsePrice(item.QuerySelector(Sel.Price)?.TextContent);
            var (plz, city) = ParseLocation(item.QuerySelector(Sel.Location)?.TextContent);
            var tagText = string.Join(' ', item.QuerySelectorAll(Sel.Tags).Select(t => t.TextContent));
            var image = ImageUrl(item.QuerySelector(Sel.Image));

            result.Add(new Listing
            {
                Platform = Platform.Kleinanzeigen,
                ExternalId = id.Trim(),
                Url = url,
                Title = Clean(titleEl.TextContent),
                Description = Clean(item.QuerySelector(Sel.Snippet)?.TextContent),
                Price = price,
                IsNegotiable = negotiable,
                PostalCode = plz,
                City = city,
                ImageUrls = image is null ? [] : [image],
                ImageCount = image is null ? 0 : 1,
                ShippingAvailable = tagText.Contains("Versand möglich", StringComparison.OrdinalIgnoreCase) ||
                                    tagText.Contains("Direkt kaufen", StringComparison.OrdinalIgnoreCase),
                PickupAvailable = !tagText.Contains("Nur Versand", StringComparison.OrdinalIgnoreCase),
                PostedAt = GermanText.ParseRelativeDate(item.QuerySelector(Sel.Date)?.TextContent),
            });
        }

        return result;
    }

    /// <summary>Detailseite in ein bestehendes Listing einarbeiten.</summary>
    public static void ApplyDetails(Listing listing, string html)
    {
        var doc = Parser.ParseDocument(html);

        if (doc.QuerySelector(Sel.DetailTitle) is { } t && !string.IsNullOrWhiteSpace(t.TextContent))
            listing.Title = Clean(t.TextContent);

        if (doc.QuerySelector(Sel.DetailPrice) is { } p)
        {
            var (price, negotiable) = GermanText.ParsePrice(p.TextContent);
            if (price is not null) listing.Price = price;
            listing.IsNegotiable = negotiable;
        }

        var (plz, city) = ParseLocation(doc.QuerySelector(Sel.DetailLocality)?.TextContent);
        listing.PostalCode = plz ?? listing.PostalCode;
        listing.City = city ?? listing.City;

        if (doc.QuerySelector(Sel.DetailDescription) is { } d)
            listing.Description = Clean(d.InnerHtml.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase), keepNewlines: true, isHtml: true);

        foreach (var attr in doc.QuerySelectorAll(Sel.DetailAttributes))
        {
            var value = attr.QuerySelector(Sel.DetailAttributeValue)?.TextContent;
            var label = attr.TextContent.Replace(value ?? string.Empty, string.Empty, StringComparison.Ordinal).Trim();
            if (label.StartsWith("Zustand", StringComparison.OrdinalIgnoreCase)) listing.Condition = GermanText.ParseCondition(value);
            else if (label.StartsWith("Marke", StringComparison.OrdinalIgnoreCase)) listing.Brand = Clean(value);
            else if (label.StartsWith("Größe", StringComparison.OrdinalIgnoreCase)) listing.Size = Clean(value);
            else if (label.StartsWith("Versand", StringComparison.OrdinalIgnoreCase) && value is not null)
                ApplyShippingText(listing, value);
        }

        var images = doc.QuerySelectorAll(Sel.DetailImages)
            .Select(ImageUrl).Where(u => u is not null).Select(u => u!).Distinct().ToList();
        if (images.Count > 0)
        {
            listing.ImageUrls = images.Take(8).ToList();
            listing.ImageCount = images.Count;
        }

        var body = doc.Body?.TextContent ?? string.Empty;
        ApplyShippingText(listing, body);
        listing.PostedAt ??= GermanText.ParseRelativeDate(doc.QuerySelector(Sel.DetailDate)?.TextContent);

        ApplySeller(listing, doc);
    }

    private static void ApplyShippingText(Listing listing, string text)
    {
        if (ShippingPriceRegex().Match(text) is { Success: true } m && GermanText.ParseDecimal(m.Groups[1].Value) is { } ship)
        {
            listing.ShippingAvailable = true;
            listing.ShippingPrice = ship;
        }

        if (text.Contains("Nur Abholung", StringComparison.OrdinalIgnoreCase)) listing.ShippingAvailable = false;
        if (text.Contains("Nur Versand", StringComparison.OrdinalIgnoreCase)) listing.PickupAvailable = false;
    }

    private static void ApplySeller(Listing listing, IDocument doc)
    {
        var box = doc.QuerySelector(Sel.SellerBox);
        if (box is null) return;

        var text = Regex.Replace(box.TextContent, @"\s+", " ");
        var seller = listing.Seller;
        seller.Name = Clean(box.QuerySelector(Sel.SellerName)?.TextContent) is { Length: > 0 } n ? n : seller.Name;

        var link = box.QuerySelector(Sel.SellerLink)?.GetAttribute("href");
        if (link is not null && UserIdRegex().Match(link) is { Success: true } uid) seller.ExternalId = uid.Groups[1].Value;

        seller.IsCommercial = text.Contains("Gewerblich", StringComparison.OrdinalIgnoreCase);
        seller.MemberSince = ActiveSinceRegex().Match(text) is { Success: true } since ? GermanText.ParseDate(since.Groups[1].Value) : null;
        seller.Badges = BadgeRegex().Matches(text).Select(m => Clean(m.Value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        seller.ActiveListings = ActiveAdsRegex().Match(text) is { Success: true } ads && int.TryParse(ads.Groups[1].Value, out var count) ? count : null;
        seller.IsLoaded = true;
    }

    internal static (string? PostalCode, string? City) ParseLocation(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);
        var clean = Clean(text);
        var m = PlzRegex().Match(clean);
        if (!m.Success) return (null, clean);
        var city = clean[(m.Index + m.Length)..].Trim(' ', '-', ',');
        return (m.Value, city.Length > 0 ? city : null);
    }

    private static string? ImageUrl(IElement? img)
    {
        if (img is null) return null;
        var src = img.GetAttribute("data-imgsrc") ?? img.GetAttribute("data-src") ?? img.GetAttribute("src") ??
                  img.GetAttribute("srcset")?.Split(',')[0].Trim().Split(' ')[0];
        if (string.IsNullOrWhiteSpace(src) || src.StartsWith("data:", StringComparison.Ordinal)) return null;
        var url = AbsoluteUrl(src);
        return url.StartsWith("https://", StringComparison.Ordinal) ? url : null;
    }

    /// <summary>Nur echte https-Links auf kleinanzeigen.de durchlassen (kein javascript:, keine Fremddomains).</summary>
    internal static bool IsOwnUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == "www.kleinanzeigen.de";

    private static string AbsoluteUrl(string href) =>
        href.StartsWith("//", StringComparison.Ordinal) ? "https:" + href :
        href.StartsWith('/') ? BaseUrl + href : href;

    private static string Clean(string? text, bool keepNewlines = false, bool isHtml = false)
    {
        if (text is null) return string.Empty;
        if (isHtml) text = Parser.ParseDocument($"<body>{text}</body>").Body?.TextContent ?? string.Empty;
        return keepNewlines
            ? string.Join('\n', text.Split('\n').Select(l => WhitespaceRegex().Replace(l, " ").Trim()).Where(l => l.Length > 0))
            : WhitespaceRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\b\d{5}\b")]
    private static partial Regex PlzRegex();

    [GeneratedRegex(@"[ \t\r\n ]+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"Versand\s+(?:ab|für)\s+([\d.,]+)\s*€", RegexOptions.IgnoreCase)]
    private static partial Regex ShippingPriceRegex();

    [GeneratedRegex(@"Aktiv seit\s+(\d{2}\.\d{2}\.\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex ActiveSinceRegex();

    [GeneratedRegex(@"(TOP|OK|Na ja)\s+Zufriedenheit|Sehr freundlich|Sehr zuverlässig|(?<!Sehr )Freundlich|(?<!Sehr )Zuverlässig", RegexOptions.IgnoreCase)]
    private static partial Regex BadgeRegex();

    [GeneratedRegex(@"(\d+)\s+Anzeigen\s+online", RegexOptions.IgnoreCase)]
    private static partial Regex ActiveAdsRegex();

    [GeneratedRegex(@"userId=(\d+)")]
    private static partial Regex UserIdRegex();
}
