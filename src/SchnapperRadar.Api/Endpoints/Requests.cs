using SchnapperRadar.Application.Search;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Api.Endpoints;

public sealed record LoginRequest(string Password);

public sealed record SearchRequest(
    string Query,
    PlatformSet? Platforms,
    decimal? MinPrice,
    decimal? MaxPrice,
    decimal? MaxTotalCost,
    DeliveryPreference? Delivery,
    double? MaxDistanceKm,
    List<ItemCondition>? Conditions,
    double? MinTrustScore,
    bool? OnlyPrivateSellers,
    bool? ExcludeDefective,
    bool? IncludeSuspicious,
    bool? UseAi,
    SortOrder? Sort)
{
    public SearchCriteria ToCriteria() => new()
    {
        Query = Query ?? string.Empty,
        Platforms = (Platforms ?? PlatformSet.All) & PlatformSet.All,
        MinPrice = MinPrice,
        MaxPrice = MaxPrice,
        MaxTotalCost = MaxTotalCost,
        Delivery = Delivery ?? DeliveryPreference.Any,
        MaxDistanceKm = MaxDistanceKm,
        Conditions = (Conditions ?? []).Distinct().Take(10).ToList(),
        MinTrustScore = MinTrustScore,
        OnlyPrivateSellers = OnlyPrivateSellers ?? false,
        ExcludeDefective = ExcludeDefective ?? true,
        IncludeSuspicious = IncludeSuspicious ?? false,
        UseAi = UseAi ?? true,
        Sort = Sort ?? SortOrder.BestDeal,
    };
}
