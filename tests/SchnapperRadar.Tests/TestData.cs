using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Tests;

internal static class TestData
{
    public static Listing Listing(string title, decimal price, Platform platform = Platform.Kleinanzeigen, string description = "",
        int images = 5, string? plz = "10117", string? id = null) => new()
    {
        Platform = platform,
        ExternalId = id ?? Guid.NewGuid().ToString("N")[..10],
        Url = platform == Platform.Vinted ? "https://www.vinted.de/items/1" : "https://www.kleinanzeigen.de/s-anzeige/x/1",
        Title = title,
        Description = description,
        Price = price,
        ImageCount = images,
        ImageUrls = images > 0 ? ["https://img.kleinanzeigen.de/x.jpg"] : [],
        PostalCode = plz,
        ShippingAvailable = true,
        PickupAvailable = platform == Platform.Kleinanzeigen,
    };

    public static SellerInfo GoodVintedSeller() => new()
    {
        IsLoaded = true, RatingPercent = 99, RatingCount = 240, SoldItems = 180,
        MemberSince = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-4)),
    };
}
