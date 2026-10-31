using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Application.Ai;

public sealed record AiAssessment(
    double Legitimacy,
    double DealQuality,
    decimal? EstimatedMarketPrice,
    AiVerdict Verdict,
    IReadOnlyList<string> RedFlags,
    string Summary,
    int InputTokens,
    int OutputTokens,
    TimeSpan Latency);
