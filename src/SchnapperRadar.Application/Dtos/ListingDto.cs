using SchnapperRadar.Application.Pipeline;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Application.Dtos;

public sealed record SellerDto(string? Name, bool IsCommercial, double? RatingPercent, int? RatingCount,
    IReadOnlyList<string> Badges, DateOnly? MemberSince, int? SoldItems, bool IsLoaded);

public sealed record CostDto(decimal Total, DeliveryMode Mode, decimal Shipping, bool ShippingIsEstimate,
    decimal BuyerFee, decimal PickupTravel, double? DistanceKm);

public sealed record ScoreDto(double Final, double Deal, double Trust, double? AiLegitimacy, double? AiDealQuality,
    PipelineStage Stage, AiVerdict AiVerdict, string? AiSummary, string? AiModel);

public sealed record ListingDto(
    Guid Id,
    Platform Platform,
    string Url,
    string Title,
    decimal? Price,
    bool IsNegotiable,
    string? ImageUrl,
    int ImageCount,
    string? City,
    string? PostalCode,
    ItemCondition Condition,
    string? Brand,
    string? Size,
    decimal? MarketReferencePrice,
    SellerDto Seller,
    CostDto Cost,
    ScoreDto Score,
    IReadOnlyList<string> RedFlags,
    DateTimeOffset? PostedAt,
    bool IsSaved)
{
    public static ListingDto From(Listing l)
    {
        var a = l.Assessment;
        var s = l.Seller;
        return new ListingDto(l.Id, l.Platform, l.Url, l.Title, l.Price, l.IsNegotiable, l.ImageUrls.FirstOrDefault(), l.ImageCount,
            l.City, l.PostalCode, l.Condition, l.Brand, l.Size, a.MarketReferencePrice,
            new SellerDto(s.Name, s.IsCommercial, s.RatingPercent, s.RatingCount, s.Badges, s.MemberSince, s.SoldItems, s.IsLoaded),
            new CostDto(a.TotalCost, a.ChosenDelivery, a.ShippingCost, a.ShippingCostIsEstimate, a.BuyerFee, a.PickupTravelCost, a.DistanceKm),
            new ScoreDto(a.FinalScore, a.DealScore, a.TrustScore, a.AiLegitimacy, a.AiDealQuality, a.Stage, a.AiVerdict, a.AiSummary, a.AiModel),
            a.RedFlags, l.PostedAt, l.IsSaved);
    }
}

public sealed record SearchResponseDto(IReadOnlyList<ListingDto> Items, PipelineStats Stats, decimal? MarketReference,
    IReadOnlyList<string> Warnings);

public sealed record FeedPageDto(IReadOnlyList<ListingDto> Items, int NextOffset, bool HasMore);

public sealed record InterestDto(Guid Id, string Query, decimal? MinPrice, decimal? MaxPrice, PlatformSet Platforms,
    bool Enabled, DateTimeOffset? LastRunAt)
{
    public static InterestDto From(Interest i) => new(i.Id, i.Query, i.MinPrice, i.MaxPrice, i.Platforms, i.Enabled, i.LastRunAt);
}

public sealed record CreateInterestRequest(string Query, decimal? MinPrice, decimal? MaxPrice, PlatformSet Platforms);

public sealed record SystemStatusDto(bool AiEnabled, string AiProvider, string AiModel, string HomePostalCode,
    AiUsageDto Usage24h, AiUsageDto Usage30d, bool BackgroundRefresh, int RefreshMinutes, bool FeedRefreshing);

public sealed record AiUsageDto(int Calls, long InputTokens, long OutputTokens);
