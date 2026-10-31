namespace SchnapperRadar.Domain.Entities;

public sealed class SellerInfo
{
    public string? ExternalId { get; set; }
    public string? Name { get; set; }
    public bool IsCommercial { get; set; }

    /// <summary>0–100, null wenn die Plattform keinen Prozentwert liefert (Kleinanzeigen).</summary>
    public double? RatingPercent { get; set; }
    public int? RatingCount { get; set; }

    /// <summary>z. B. "TOP Zufriedenheit", "Sehr zuverlässig".</summary>
    public List<string> Badges { get; set; } = [];
    public DateOnly? MemberSince { get; set; }
    public int? ActiveListings { get; set; }
    public int? SoldItems { get; set; }
    public bool IsLoaded { get; set; }
}
