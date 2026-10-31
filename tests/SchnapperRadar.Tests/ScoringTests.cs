using SchnapperRadar.Application.Scoring;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Tests;

public class RedFlagDetectorTests
{
    [Theory]
    [InlineData("Nike Dunk Low 1:1 Qualität", "")]
    [InlineData("Rolex Submariner Replica", "")]
    [InlineData("iPhone 15 Pro", "Zahlung bitte per Western Union")]
    [InlineData("PS5", "Bezahlung mit Amazon Gutschein")]
    public void Hard_flags_exclude(string title, string description)
    {
        var flags = RedFlagDetector.DetectText(TestData.Listing(title, 100, description: description), "x");
        Assert.Contains(flags, f => f.Severity == RedFlagSeverity.Hard);
    }

    [Theory]
    [InlineData("Bitte nur per WhatsApp melden 0176 1234567")]
    [InlineData("Bin gerade auf Montage, Versand per Spedition")]
    [InlineData("Nur PayPal Freunde und Familie")]
    [InlineData("Schreib mir eine Mail an max@example.com")]
    public void Scam_patterns_are_major(string description)
    {
        var flags = RedFlagDetector.DetectText(TestData.Listing("MacBook Air M2", 500, description: description), "macbook");
        Assert.Contains(flags, f => f.Severity == RedFlagSeverity.Major);
    }

    [Fact]
    public void Wanted_ads_are_excluded()
    {
        var flags = RedFlagDetector.DetectText(TestData.Listing("Suche iPhone 13", 300), "iphone 13");
        Assert.Contains(flags, f => f.Code == RedFlagCodes.WantedAd && f.Severity == RedFlagSeverity.Hard);
    }

    [Fact]
    public void Accessory_is_only_flagged_if_not_searched_for()
    {
        Assert.True(RedFlagDetector.IsAccessory("iPhone 13 Hülle schwarz", "iphone 13"));
        Assert.False(RedFlagDetector.IsAccessory("iPhone 13 Hülle schwarz", "iphone 13 hülle"));
    }

    [Fact]
    public void Clean_listing_has_no_flags()
    {
        var l = TestData.Listing("Sony WH-1000XM5 Kopfhörer", 180, description: "Wenig benutzt, mit Rechnung und OVP. Abholung oder Versand.");
        Assert.Empty(RedFlagDetector.DetectText(l, "sony wh-1000xm5"));
        Assert.Empty(RedFlagDetector.DetectStructural(l, 200));
    }

    [Fact]
    public void Suspiciously_cheap_is_major_unless_seller_is_very_trusted()
    {
        var l = TestData.Listing("PS5 Disc", 90, Platform.Vinted);
        Assert.Contains(RedFlagDetector.DetectStructural(l, 400), f => f is { Code: RedFlagCodes.SuspiciouslyCheap, Severity: RedFlagSeverity.Major });

        l.Seller = TestData.GoodVintedSeller();
        Assert.Contains(RedFlagDetector.DetectStructural(l, 400), f => f is { Code: RedFlagCodes.SuspiciouslyCheap, Severity: RedFlagSeverity.Minor });
    }

    [Fact]
    public void New_account_with_expensive_item_is_major()
    {
        var l = TestData.Listing("RTX 4090", 900);
        l.Seller = new SellerInfo { IsLoaded = true, MemberSince = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)) };
        Assert.Contains(RedFlagDetector.DetectStructural(l, 1500), f => f.Code == RedFlagCodes.NewAccountHighValue);
    }
}

public class TrustScorerTests
{
    [Fact]
    public void Unloaded_seller_is_neutral_skeptical() =>
        Assert.Equal(TrustScorer.Unknown, TrustScorer.Score(new SellerInfo(), Platform.Vinted));

    [Fact]
    public void Vinted_many_good_ratings_score_high() =>
        Assert.True(TrustScorer.Score(TestData.GoodVintedSeller(), Platform.Vinted) > 90);

    [Fact]
    public void Vinted_few_ratings_are_damped()
    {
        var few = new SellerInfo { IsLoaded = true, RatingPercent = 100, RatingCount = 2 };
        var many = new SellerInfo { IsLoaded = true, RatingPercent = 100, RatingCount = 200 };
        Assert.True(TrustScorer.Score(few, Platform.Vinted) < TrustScorer.Score(many, Platform.Vinted) - 30);
    }

    [Fact]
    public void Kleinanzeigen_badges_and_age_count()
    {
        var top = new SellerInfo
        {
            IsLoaded = true, Badges = ["TOP Zufriedenheit", "Sehr freundlich", "Sehr zuverlässig"],
            MemberSince = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-8)),
        };
        var fresh = new SellerInfo { IsLoaded = true, MemberSince = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)) };
        Assert.True(TrustScorer.Score(top, Platform.Kleinanzeigen) >= 95);
        Assert.True(TrustScorer.Score(fresh, Platform.Kleinanzeigen) <= 25);
    }
}

public class MarketPriceTests
{
    [Fact]
    public void Trimmed_median_ignores_accessories_and_outliers()
    {
        var listings = new[] { 400m, 420m, 410m, 390m, 450m, 380m, 430m, 405m, 415m, 9999m, 1m }
            .Select(p => TestData.Listing("iPhone 14", p)).ToList();
        listings.Add(TestData.Listing("iPhone 14 Hülle", 5));

        var median = MarketPriceEstimator.Estimate(listings, "iphone 14");
        Assert.InRange(median!.Value, 400, 420);
    }

    [Fact]
    public void Blend_ignores_implausible_ai_estimate()
    {
        Assert.Equal(400, MarketPriceEstimator.Blend(400, 5000));
        Assert.Equal(450, MarketPriceEstimator.Blend(400, 500));
        Assert.Equal(300, MarketPriceEstimator.Blend(null, 300));
    }
}

public class DealScorerTests
{
    [Fact]
    public void Deal_score_scale()
    {
        Assert.Equal(50, DealScorer.DealScore(100, 100));
        Assert.Equal(100, DealScorer.DealScore(60, 100), 6);
        Assert.Equal(0, DealScorer.DealScore(140, 100), 6);
        Assert.Equal(50, DealScorer.DealScore(100, null));
    }

    [Fact]
    public void Ai_avoid_caps_score_even_with_great_heuristics()
    {
        var a = new DealAssessment
        {
            DealScore = 100, TrustScore = 100, AiLegitimacy = 100, AiDealQuality = 100, AiVerdict = AiVerdict.Avoid,
        };
        Assert.Equal(25, DealScorer.FinalScore(a, []));
    }

    [Fact]
    public void Ai_cannot_erase_heuristic_penalties()
    {
        var flags = new[] { new Domain.ValueObjects.RedFlag("x", "x", RedFlagSeverity.Major), new Domain.ValueObjects.RedFlag("y", "y", RedFlagSeverity.Major) };
        var a = new DealAssessment { DealScore = 80, TrustScore = 80, AiLegitimacy = 95, AiDealQuality = 90, AiVerdict = AiVerdict.Check };
        Assert.True(DealScorer.FinalScore(a, flags) <= DealScorer.FinalScore(a, []) - 29);
    }
}
