using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Domain.Entities;

public sealed class DealAssessment
{
    public PipelineStage Stage { get; set; } = PipelineStage.Collected;

    // --- Kosten ---
    public decimal TotalCost { get; set; }
    public DeliveryMode ChosenDelivery { get; set; }
    public decimal ShippingCost { get; set; }
    public bool ShippingCostIsEstimate { get; set; }
    public decimal BuyerFee { get; set; }
    public decimal PickupTravelCost { get; set; }
    public double? DistanceKm { get; set; }

    // --- Heuristik ---
    public decimal? MarketReferencePrice { get; set; }
    public double DealScore { get; set; }
    public double TrustScore { get; set; }
    public double PreScore { get; set; }
    public List<string> RedFlags { get; set; } = [];
    public bool IsExcluded { get; set; }

    // --- KI ---
    public double? AiLegitimacy { get; set; }
    public double? AiDealQuality { get; set; }
    public decimal? AiEstimatedMarketPrice { get; set; }
    public AiVerdict AiVerdict { get; set; } = AiVerdict.NotAssessed;
    public string? AiSummary { get; set; }
    public string? AiModel { get; set; }
    public string? AiContentHash { get; set; }

    public double FinalScore { get; set; }
    public DateTimeOffset AssessedAt { get; set; }
}
