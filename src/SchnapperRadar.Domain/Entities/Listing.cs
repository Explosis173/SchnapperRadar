using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Domain.Entities;

public sealed class Listing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Platform Platform { get; set; }
    public required string ExternalId { get; set; }
    public required string Url { get; set; }
    public required string Title { get; set; }
    public string Description { get; set; } = string.Empty;

    public decimal? Price { get; set; }
    public bool IsNegotiable { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public int ImageCount { get; set; }

    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public ItemCondition Condition { get; set; }
    public string? Brand { get; set; }
    public string? Size { get; set; }

    public bool ShippingAvailable { get; set; }
    public bool PickupAvailable { get; set; }
    /// <summary>Vom Inserat angegebener Versandpreis (null = unbekannt, wird geschätzt).</summary>
    public decimal? ShippingPrice { get; set; }
    /// <summary>Von der Plattform berechnete Käuferschutz-Gebühr (null = unbekannt, wird berechnet).</summary>
    public decimal? PlatformBuyerFee { get; set; }
    /// <summary>Vinted package_size_id o. ä. (1 = klein, 2 = mittel, 3 = groß).</summary>
    public int? PackageSize { get; set; }

    public SellerInfo Seller { get; set; } = new();
    public DealAssessment Assessment { get; set; } = new();

    public DateTimeOffset? PostedAt { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DetailLoadedAt { get; set; }
    public bool IsDetailLoaded => DetailLoadedAt is not null;

    /// <summary>Für den Feed: aus welchem Interesse kam das Inserat?</summary>
    public Guid? InterestId { get; set; }
    public bool IsHidden { get; private set; }
    public bool IsSaved { get; private set; }

    public void Hide() => IsHidden = true;
    public void ToggleSaved() => IsSaved = !IsSaved;

    /// <summary>Übernimmt frische Scrape-Daten. Nutzerstatus, Detaildaten und KI-Bewertung bleiben erhalten.</summary>
    public void MergeFrom(Listing fresh)
    {
        Title = fresh.Title;
        Url = fresh.Url;
        Price = fresh.Price;
        IsNegotiable = fresh.IsNegotiable;
        if (fresh.ImageUrls.Count > 0) ImageUrls = fresh.ImageUrls;
        ImageCount = Math.Max(ImageCount, fresh.ImageCount);
        if (fresh.Description.Length > Description.Length) Description = fresh.Description;
        PostalCode = fresh.PostalCode ?? PostalCode;
        City = fresh.City ?? City;
        if (fresh.Condition != ItemCondition.Unknown) Condition = fresh.Condition;
        Brand = fresh.Brand ?? Brand;
        Size = fresh.Size ?? Size;
        ShippingAvailable = fresh.ShippingAvailable;
        PickupAvailable = fresh.PickupAvailable;
        ShippingPrice = fresh.ShippingPrice ?? ShippingPrice;
        PlatformBuyerFee = fresh.PlatformBuyerFee ?? PlatformBuyerFee;
        PackageSize = fresh.PackageSize ?? PackageSize;
        Seller.ExternalId ??= fresh.Seller.ExternalId;
        Seller.Name ??= fresh.Seller.Name;
        if (fresh.Seller.IsLoaded) Seller = fresh.Seller;
        PostedAt = fresh.PostedAt ?? PostedAt;
        LastSeenAt = DateTimeOffset.UtcNow;
        InterestId ??= fresh.InterestId;
    }
}
