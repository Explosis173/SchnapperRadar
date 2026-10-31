using SchnapperRadar.Application.Options;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Domain.ValueObjects;

namespace SchnapperRadar.Application.Costs;

public sealed record CostBreakdown(
    bool IsDeliverable,
    DeliveryMode Mode,
    decimal Total,
    decimal Shipping,
    bool ShippingIsEstimate,
    decimal BuyerFee,
    decimal PickupTravel,
    double? DistanceKm);

/// <summary>Was kostet mich das Teil wirklich bis zur Haustür?</summary>
public sealed class CostCalculator(HomeOptions home, CostOptions costs)
{
    public CostBreakdown Calculate(Listing listing, GeoPoint? homePoint, GeoPoint? listingPoint, DeliveryPreference preference,
        double? maxPickupKm = null)
    {
        var price = listing.Price ?? 0;
        double? distance = homePoint is { } h && listingPoint is { } l
            ? Math.Round(h.DistanceKmTo(l) * home.RoadFactor, 1)
            : null;

        CostBreakdown? shipping = null;
        if (listing.ShippingAvailable && preference != DeliveryPreference.PickupOnly)
        {
            var isEstimate = listing.ShippingPrice is null;
            var ship = listing.ShippingPrice ?? EstimateShipping(listing);
            var fee = listing.PlatformBuyerFee ?? BuyerFee(listing.Platform, price);
            shipping = new CostBreakdown(true, DeliveryMode.Shipping, price + ship + fee, ship, isEstimate, fee, 0, distance);
        }

        CostBreakdown? pickup = null;
        if (listing.PickupAvailable && preference != DeliveryPreference.ShippingOnly &&
            (distance is null || distance <= (maxPickupKm ?? home.MaxPickupDistanceKm)))
        {
            var travel = distance is { } d
                ? Math.Round(2 * (decimal)d * home.CarCostPerKm, 2)
                : costs.UnknownPickupEstimate;
            pickup = new CostBreakdown(true, DeliveryMode.Pickup, price + travel, 0, distance is null, 0, travel, distance);
        }

        return (shipping, pickup) switch
        {
            ({ } s, { } p) => s.Total <= p.Total ? s : p,
            ({ } s, null) => s,
            (null, { } p) => p,
            _ => new CostBreakdown(false, DeliveryMode.Unknown, price, 0, false, 0, 0, distance),
        };
    }

    public decimal BuyerFee(Platform platform, decimal price) => platform switch
    {
        Platform.Vinted => Math.Round(costs.VintedBuyerFeeFixed + price * costs.VintedBuyerFeePercent / 100m, 2),
        Platform.Kleinanzeigen when costs.KleinanzeigenBuyerFeeFixed > 0 || costs.KleinanzeigenBuyerFeePercent > 0 =>
            Math.Round(costs.KleinanzeigenBuyerFeeFixed + price * costs.KleinanzeigenBuyerFeePercent / 100m, 2),
        _ => 0,
    };

    private decimal EstimateShipping(Listing listing) => listing.Platform switch
    {
        Platform.Vinted => listing.PackageSize switch
        {
            1 => costs.VintedShippingSmall,
            3 => costs.VintedShippingLarge,
            _ => costs.VintedShippingMedium,
        },
        _ => costs.KleinanzeigenShippingEstimate,
    };
}
