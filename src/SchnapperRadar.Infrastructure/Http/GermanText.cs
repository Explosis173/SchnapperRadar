using System.Globalization;
using System.Text.RegularExpressions;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Infrastructure.Http;

internal static partial class GermanText
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>"1.234 € VB" → 1234, "Zu verschenken" → 0, "VB" → null.</summary>
    public static (decimal? Price, bool Negotiable) ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, false);
        var t = text.Trim();
        var negotiable = t.Contains("VB", StringComparison.Ordinal);
        if (t.Contains("verschenken", StringComparison.OrdinalIgnoreCase)) return (0m, false);

        var m = PriceRegex().Match(t);
        if (!m.Success) return (null, negotiable);
        return decimal.TryParse(m.Value, NumberStyles.Number, German, out var price) ? (price, negotiable) : (null, negotiable);
    }

    public static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = PriceRegex().Match(text);
        return m.Success && decimal.TryParse(m.Value, NumberStyles.Number, German, out var d) ? d : null;
    }

    /// <summary>"Heute, 10:12" / "Gestern, 18:30" / "12.10.2026".</summary>
    public static DateTimeOffset? ParseRelativeDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        var nowBerlin = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Berlin);
        var time = TimeRegex().Match(t);
        var (h, min) = time.Success ? (int.Parse(time.Groups[1].Value, German), int.Parse(time.Groups[2].Value, German)) : (12, 0);

        DateTime? day = null;
        if (t.StartsWith("Heute", StringComparison.OrdinalIgnoreCase)) day = nowBerlin.Date;
        else if (t.StartsWith("Gestern", StringComparison.OrdinalIgnoreCase)) day = nowBerlin.Date.AddDays(-1);
        else if (DateRegex().Match(t) is { Success: true } d &&
                 DateTime.TryParseExact(d.Value, "dd.MM.yyyy", German, DateTimeStyles.None, out var parsed)) day = parsed;

        if (day is null) return null;
        var local = day.Value.AddHours(h).AddMinutes(min);
        return new DateTimeOffset(local, Berlin.GetUtcOffset(local));
    }

    public static DateOnly? ParseDate(string? text) =>
        text is not null && DateRegex().Match(text) is { Success: true } m &&
        DateOnly.TryParseExact(m.Value, "dd.MM.yyyy", German, DateTimeStyles.None, out var d) ? d : null;

    public static ItemCondition ParseCondition(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        null or "" => ItemCondition.Unknown,
        var t when t.Contains("neu mit etikett") => ItemCondition.NewWithTags,
        var t when t.StartsWith("neu") => ItemCondition.New,
        var t when t.Contains("sehr gut") || t.Contains("wie neu") => ItemCondition.VeryGood,
        var t when t.Contains("gut") => ItemCondition.Good,
        var t when t.Contains("in ordnung") || t.Contains("zufriedenstellend") || t.Contains("akzeptabel") => ItemCondition.Acceptable,
        var t when t.Contains("defekt") => ItemCondition.Defective,
        _ => ItemCondition.Unknown,
    };

    [GeneratedRegex(@"\d{1,3}(?:\.\d{3})+(?:,\d{1,2})?|\d+(?:,\d{1,2})?")]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"(\d{1,2}):(\d{2})")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"\d{2}\.\d{2}\.\d{4}")]
    private static partial Regex DateRegex();
}
