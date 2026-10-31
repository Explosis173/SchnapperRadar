using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Application.Scoring;

/// <summary>Verkäufer-Vertrauen 0–100. Unbekannt = 45 (leicht skeptisch).</summary>
public static class TrustScorer
{
    public const double Unknown = 45;

    public static double Score(SellerInfo seller, Platform platform)
    {
        if (!seller.IsLoaded) return Unknown;

        var score = platform switch
        {
            Platform.Vinted => VintedScore(seller),
            Platform.Kleinanzeigen => KleinanzeigenScore(seller),
            _ => Unknown,
        };

        if (seller.MemberSince is { } since)
        {
            var days = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - since.DayNumber;
            score += days switch
            {
                < 30 => -20,
                < 180 => -5,
                _ => Math.Min(20, days / 365.0 * 4),
            };
        }

        return Math.Clamp(score, 0, 100);
    }

    private static double VintedScore(SellerInfo seller)
    {
        var count = seller.RatingCount ?? 0;
        if (count == 0 || seller.RatingPercent is null) return 35;

        // Viele Bewertungen = Prozentwert ist belastbar. 50+ Bewertungen = volle Gewichtung.
        var confidence = Math.Min(1.0, Math.Log10(count + 1) / Math.Log10(51));
        var score = 35 + (seller.RatingPercent.Value - 35) * confidence;
        if (seller.SoldItems is > 20) score += 5;
        return score;
    }

    private static double KleinanzeigenScore(SellerInfo seller)
    {
        double score = 45;
        foreach (var badge in seller.Badges.Select(b => b.ToLowerInvariant()))
        {
            score += badge switch
            {
                _ when badge.Contains("top") => 25,
                _ when badge.Contains("na ja") => -20,
                _ when badge.Contains("ok zufriedenheit") || badge == "ok" => 8,
                _ when badge.Contains("sehr zuverlässig") => 10,
                _ when badge.Contains("zuverlässig") => 6,
                _ when badge.Contains("sehr freundlich") => 8,
                _ when badge.Contains("freundlich") => 5,
                _ => 0,
            };
        }
        if (seller.IsCommercial) score += 5; // Gewährleistung + Impressum
        return score;
    }
}
