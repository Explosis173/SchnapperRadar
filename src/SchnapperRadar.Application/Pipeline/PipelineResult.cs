using SchnapperRadar.Domain.Entities;

namespace SchnapperRadar.Application.Pipeline;

public sealed record PipelineStats(
    int Collected,
    int PassedPrefilter,
    int Enriched,
    int AiAssessed,
    int AiFromCache,
    int Excluded);

public sealed record PipelineResult(
    IReadOnlyList<Listing> Items,
    PipelineStats Stats,
    decimal? MarketReference,
    IReadOnlyList<string> Warnings);
