using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Application.Search;

public enum SortOrder
{
    BestDeal = 0,
    TotalCostAsc = 1,
    Newest = 2,
    MostTrusted = 3,
}

public sealed record SearchCriteria
{
    public required string Query { get; init; }
    public PlatformSet Platforms { get; init; } = PlatformSet.All;
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    /// <summary>Inkl. Versand, Gebühren bzw. Fahrtkosten.</summary>
    public decimal? MaxTotalCost { get; init; }
    public DeliveryPreference Delivery { get; init; } = DeliveryPreference.Any;
    public double? MaxDistanceKm { get; init; }
    public IReadOnlyList<ItemCondition> Conditions { get; init; } = [];
    public double? MinTrustScore { get; init; }
    public bool OnlyPrivateSellers { get; init; }
    public bool ExcludeDefective { get; init; } = true;
    public bool IncludeSuspicious { get; init; }
    public bool UseAi { get; init; } = true;
    public SortOrder Sort { get; init; } = SortOrder.BestDeal;
}

/// <summary>Was an die Quelle selbst geht (alles andere wird nachgefiltert).</summary>
public sealed record SourceQuery
{
    public required string Query { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int MaxResults { get; init; } = 60;
    public IReadOnlyList<ItemCondition> Conditions { get; init; } = [];
    public bool OnlyPrivateSellers { get; init; }
    /// <summary>Für reine Abholung: Umkreissuche ab dieser PLZ.</summary>
    public string? NearPostalCode { get; init; }
    public int? RadiusKm { get; init; }
}
