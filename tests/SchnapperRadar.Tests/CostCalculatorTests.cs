using SchnapperRadar.Application.Costs;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Domain.ValueObjects;

namespace SchnapperRadar.Tests;

public class CostCalculatorTests
{
    private static readonly GeoPoint Berlin = new(52.52, 13.40);
    private static readonly GeoPoint Potsdam = new(52.39, 13.06);   // ~27 km Luftlinie
    private static readonly GeoPoint Munich = new(48.14, 11.58);

    private readonly CostCalculator _calc = new(new HomeOptions(), new CostOptions());

    [Fact]
    public void Vinted_adds_buyer_protection_and_estimated_shipping()
    {
        var l = TestData.Listing("Jacke", 20, Platform.Vinted);
        var c = _calc.Calculate(l, Berlin, null, DeliveryPreference.Any);

        Assert.Equal(DeliveryMode.Shipping, c.Mode);
        Assert.Equal(1.70m, c.BuyerFee);          // 0,70 € + 5 %
        Assert.True(c.ShippingIsEstimate);
        Assert.Equal(20 + 1.70m + 4.99m, c.Total);
    }

    [Fact]
    public void Uses_platform_fee_and_real_shipping_when_known()
    {
        var l = TestData.Listing("Jacke", 20, Platform.Vinted);
        l.PlatformBuyerFee = 1.50m;
        l.ShippingPrice = 3.20m;
        var c = _calc.Calculate(l, Berlin, null, DeliveryPreference.Any);
        Assert.Equal(24.70m, c.Total);
        Assert.False(c.ShippingIsEstimate);
    }

    [Fact]
    public void Picks_cheaper_of_pickup_and_shipping()
    {
        var l = TestData.Listing("Sofa", 100);
        l.ShippingPrice = 49m;
        var c = _calc.Calculate(l, Berlin, Potsdam, DeliveryPreference.Any);
        Assert.Equal(DeliveryMode.Pickup, c.Mode);
        Assert.InRange(c.PickupTravel, 15m, 30m);   // 2 × ~35 km Straße × 0,30 €
    }

    [Fact]
    public void Pickup_beyond_max_distance_is_not_offered()
    {
        var l = TestData.Listing("Sofa", 100);
        l.ShippingAvailable = false;
        var c = _calc.Calculate(l, Berlin, Munich, DeliveryPreference.Any);
        Assert.False(c.IsDeliverable);
    }

    [Fact]
    public void Shipping_only_preference_ignores_pickup()
    {
        var l = TestData.Listing("Lampe", 30);
        var c = _calc.Calculate(l, Berlin, Berlin, DeliveryPreference.ShippingOnly);
        Assert.Equal(DeliveryMode.Shipping, c.Mode);
    }
}
