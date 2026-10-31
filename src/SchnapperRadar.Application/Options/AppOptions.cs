namespace SchnapperRadar.Application.Options;

public sealed class HomeOptions
{
    public const string Section = "Home";

    /// <summary>Deine PLZ – Basis für Abhol-Entfernung.</summary>
    public string PostalCode { get; set; } = "10115";
    /// <summary>Kosten pro gefahrenem km (Sprit + Verschleiß).</summary>
    public decimal CarCostPerKm { get; set; } = 0.30m;
    /// <summary>Luftlinie → Straße.</summary>
    public double RoadFactor { get; set; } = 1.3;
    public double MaxPickupDistanceKm { get; set; } = 50;
}

public sealed class CostOptions
{
    public const string Section = "Costs";

    /// <summary>Wenn Kleinanzeigen keinen Versandpreis nennt.</summary>
    public decimal KleinanzeigenShippingEstimate { get; set; } = 6.99m;
    /// <summary>Käuferschutz bei "Sicher bezahlen" – trag hier die aktuelle Gebühr ein, falls du das nutzt.</summary>
    public decimal KleinanzeigenBuyerFeeFixed { get; set; }
    public decimal KleinanzeigenBuyerFeePercent { get; set; }

    public decimal VintedShippingSmall { get; set; } = 3.49m;
    public decimal VintedShippingMedium { get; set; } = 4.99m;
    public decimal VintedShippingLarge { get; set; } = 7.99m;
    /// <summary>Käuferschutz Vinted (Fallback, falls die API ihn nicht mitliefert).</summary>
    public decimal VintedBuyerFeeFixed { get; set; } = 0.70m;
    public decimal VintedBuyerFeePercent { get; set; } = 5m;

    /// <summary>Wenn keine Entfernung bekannt ist.</summary>
    public decimal UnknownPickupEstimate { get; set; } = 10m;
}

public sealed class PipelineOptions
{
    public const string Section = "Pipeline";

    public int MaxResultsPerSource { get; set; } = 60;
    /// <summary>Stufe 2: so viele Top-Kandidaten bekommen Detailseite + Verkäuferprofil.</summary>
    public int EnrichTopK { get; set; } = 20;
    /// <summary>Stufe 3: so viele davon gehen an die KI.</summary>
    public int AiTopK { get; set; } = 8;
    /// <summary>Unter diesem Vertrauenswert wird gar nicht erst KI-Geld ausgegeben.</summary>
    public double MinTrustForAi { get; set; } = 40;
    public int AiConcurrency { get; set; } = 3;
    public int EnrichConcurrency { get; set; } = 2;
    /// <summary>Details nicht neu laden, wenn jünger als das.</summary>
    public int DetailCacheHours { get; set; } = 24;
}

public sealed class FeedOptions
{
    public const string Section = "Feed";

    public bool BackgroundRefreshEnabled { get; set; } = true;
    public int RefreshMinutes { get; set; } = 60;
    public int KeepDays { get; set; } = 14;
    /// <summary>Nur Inserate ab diesem Endscore landen im Feed.</summary>
    public double MinScore { get; set; } = 55;
}
