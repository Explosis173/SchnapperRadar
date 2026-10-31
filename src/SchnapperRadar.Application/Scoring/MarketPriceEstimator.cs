using SchnapperRadar.Domain.Entities;

namespace SchnapperRadar.Application.Scoring;

/// <summary>
/// Schätzt den "normalen" Preis aus allen Treffern derselben Suche (getrimmter Median),
/// damit wir wissen, was überhaupt ein Schnapper ist.
/// </summary>
public static class MarketPriceEstimator
{
    public static decimal? Estimate(IEnumerable<Listing> listings, string query)
    {
        var prices = listings
            .Where(l => l.Price is > 0 && !RedFlagDetector.IsAccessory(l.Title, query))
            .Select(l => l.Price!.Value)
            .Order()
            .ToList();

        if (prices.Count < 4) return prices.Count == 0 ? null : Median(prices);

        // Unteres und oberes Zehntel weg – da sitzen Fakes, Zubehör und Mondpreise.
        var trim = prices.Count / 10;
        return Median(prices.Skip(trim).Take(prices.Count - 2 * trim).ToList());
    }

    /// <summary>KI-Schätzung einfließen lassen, aber nur wenn sie plausibel ist.</summary>
    public static decimal? Blend(decimal? median, decimal? aiEstimate)
    {
        if (aiEstimate is not > 0) return median;
        if (median is not > 0) return aiEstimate;
        var ratio = aiEstimate.Value / median.Value;
        return ratio is < 0.33m or > 3m ? median : Math.Round((median.Value + aiEstimate.Value) / 2, 2);
    }

    private static decimal Median(List<decimal> sorted)
    {
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }
}
