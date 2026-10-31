using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Domain.ValueObjects;

namespace SchnapperRadar.Application.Scoring;

public static class DealScorer
{
    private const double MajorPenalty = 15;
    private const double MinorPenalty = 5;

    /// <summary>50 = Marktpreis, 100 = ≥40 % günstiger, 0 = ≥40 % teurer.</summary>
    public static double DealScore(decimal totalCost, decimal? marketReference)
    {
        if (marketReference is not > 0) return 50;
        var ratio = (double)(totalCost / marketReference.Value);
        return Math.Clamp(50 + (1 - ratio) * 125, 0, 100);
    }

    public static double Penalty(IEnumerable<RedFlag> flags) =>
        flags.Sum(f => f.Severity switch
        {
            RedFlagSeverity.Major => MajorPenalty,
            RedFlagSeverity.Minor => MinorPenalty,
            _ => 0,
        });

    /// <summary>Stufe 1/2: rein heuristisch.</summary>
    public static double HeuristicScore(double deal, double trust, IReadOnlyCollection<RedFlag> flags) =>
        Math.Clamp(0.55 * deal + 0.45 * trust - Penalty(flags), 0, 100);

    /// <summary>Stufe 3: Heuristik + KI. Die KI kann nie allein einen Deal "retten" – Strafpunkte bleiben.</summary>
    public static double FinalScore(DealAssessment a, IReadOnlyCollection<RedFlag> flags)
    {
        if (a.AiVerdict == AiVerdict.NotAssessed || a.AiLegitimacy is null || a.AiDealQuality is null)
            return HeuristicScore(a.DealScore, a.TrustScore, flags);

        var score = 0.35 * a.DealScore + 0.25 * a.TrustScore + 0.25 * a.AiLegitimacy.Value + 0.15 * a.AiDealQuality.Value
                    - Penalty(flags);
        score = a.AiVerdict switch
        {
            AiVerdict.Avoid => Math.Min(score, 25),
            AiVerdict.Buy => score + 5,
            _ => score,
        };
        return Math.Clamp(score, 0, 100);
    }
}
