using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Infrastructure.Http;
using SchnapperRadar.Infrastructure.Sources.Kleinanzeigen;
using SchnapperRadar.Infrastructure.Sources.Vinted;

namespace SchnapperRadar.Tests;

public class KleinanzeigenParserTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Parses_search_results_and_rejects_bad_urls()
    {
        var items = KleinanzeigenParser.ParseSearchResults(Fixture("ka_search.html"));

        Assert.Equal(2, items.Count);
        var phone = items[0];
        Assert.Equal("3012345678", phone.ExternalId);
        Assert.Equal("iPhone 14 128GB Schwarz", phone.Title);
        Assert.Equal(1050m, phone.Price);
        Assert.True(phone.IsNegotiable);
        Assert.Equal("10117", phone.PostalCode);
        Assert.Equal("Mitte", phone.City);
        Assert.True(phone.ShippingAvailable);
        Assert.StartsWith("https://www.kleinanzeigen.de/s-anzeige/", phone.Url);
        Assert.Single(phone.ImageUrls);

        var sofa = items[1];
        Assert.Equal(0m, sofa.Price);
        Assert.False(sofa.ShippingAvailable);
        Assert.Equal(0, sofa.ImageCount);
    }

    [Fact]
    public void Applies_detail_page()
    {
        var l = KleinanzeigenParser.ParseSearchResults(Fixture("ka_search.html"))[0];
        KleinanzeigenParser.ApplyDetails(l, Fixture("ka_detail.html"));

        Assert.Equal(6.19m, l.ShippingPrice);
        Assert.Equal(ItemCondition.VeryGood, l.Condition);
        Assert.Equal("Apple", l.Brand);
        Assert.Equal(3, l.ImageCount);
        Assert.Contains("Akku 91 %", l.Description);
        Assert.Contains('\n', l.Description);
        Assert.True(l.Seller.IsLoaded);
        Assert.Equal("4711", l.Seller.ExternalId);
        Assert.False(l.Seller.IsCommercial);
        Assert.Equal(new DateOnly(2016, 5, 3), l.Seller.MemberSince);
        Assert.Contains("TOP Zufriedenheit", l.Seller.Badges);
        Assert.Contains("Sehr freundlich", l.Seller.Badges);
        Assert.Contains("Zuverlässig", l.Seller.Badges);
        Assert.Equal(12, l.Seller.ActiveListings);
    }

    [Fact]
    public void Builds_search_url()
    {
        var url = KleinanzeigenSource.BuildSearchUrl(new() { Query = "ps5 disc", MaxPrice = 400, NearPostalCode = "10115", RadiusKm = 30 }, 2);
        Assert.Contains("keywords=ps5%20disc", url);
        Assert.Contains("maxPrice=400", url);
        Assert.Contains("pageNum=2", url);
        Assert.Contains("adType=OFFER", url);
        Assert.Contains("radius=30", url);
    }

    [Theory]
    [InlineData("1.234 € VB", 1234, true)]
    [InlineData("49,90 €", 49.90, false)]
    [InlineData("Zu verschenken", 0, false)]
    public void Parses_german_prices(string text, double expected, bool vb)
    {
        var (price, negotiable) = GermanText.ParsePrice(text);
        Assert.Equal((decimal)expected, price);
        Assert.Equal(vb, negotiable);
    }

    [Fact]
    public void Price_only_vb_is_unknown() => Assert.Null(GermanText.ParsePrice("VB").Price);
}

public class VintedMapperTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Parses_catalog_with_both_price_formats()
    {
        var items = VintedMapper.ParseCatalog(Fixture("vinted_catalog.json"), "www.vinted.de");
        Assert.Equal(2, items.Count);

        var shoe = items[0];
        Assert.Equal(35m, shoe.Price);
        Assert.Equal(2.45m, shoe.PlatformBuyerFee);
        Assert.Equal(ItemCondition.VeryGood, shoe.Condition);
        Assert.Equal("987", shoe.Seller.ExternalId);
        Assert.Equal("https://www.vinted.de/items/5123456789-nike-air-max-90", shoe.Url);
        Assert.NotNull(shoe.PostedAt);

        var jacket = items[1];
        Assert.Equal(12m, jacket.Price);
        Assert.Equal(ItemCondition.NewWithTags, jacket.Condition);
        Assert.Equal("https://www.vinted.de/items/5123456790", jacket.Url); // javascript:-URL verworfen
        Assert.True(jacket.Seller.IsCommercial);
    }

    [Fact]
    public void Applies_user_profile()
    {
        var l = VintedMapper.ParseCatalog(Fixture("vinted_catalog.json"), "www.vinted.de")[0];
        VintedMapper.ApplyUser(l, Fixture("vinted_user.json"));
        Assert.True(l.Seller.IsLoaded);
        Assert.Equal(98, l.Seller.RatingPercent);
        Assert.Equal(143, l.Seller.RatingCount);
        Assert.Equal(120, l.Seller.SoldItems);
        Assert.Equal(new DateOnly(2019, 3, 1), l.Seller.MemberSince);
    }
}
