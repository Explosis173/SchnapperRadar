using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Ai;
using SchnapperRadar.Application.Costs;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Application.Scoring;
using SchnapperRadar.Application.Search;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Domain.ValueObjects;

namespace SchnapperRadar.Application.Pipeline;

/// <summary>
/// Der Trichter:
///  1. Suchergebnisse laden (billig)  → Heuristik-Score für alle, harte Fakes + Filter raus
///  2. Top-K anreichern (Detailseite, Verkäuferprofil, echte Versandkosten) → neu bewerten
///  3. Nur die besten vertrauenswürdigen Kandidaten an die KI (kostet Geld/Zeit) → Endscore
/// </summary>
public sealed class DealPipeline(
    IEnumerable<IListingSource> sources,
    IGeoService geo,
    AiDealAssessor ai,
    CostCalculator costCalculator,
    IListingRepository listingRepository,
    IAiUsageRepository usageRepository,
    IUnitOfWork unitOfWork,
    IOptions<HomeOptions> homeOptions,
    IOptions<PipelineOptions> pipelineOptions,
    ILogger<DealPipeline> logger)
{
    private const string AiFlagPrefix = "KI: ";
    private readonly HomeOptions _home = homeOptions.Value;
    private readonly PipelineOptions _options = pipelineOptions.Value;

    private sealed class Candidate(Listing listing, bool isNew)
    {
        public Listing Listing { get; } = listing;
        public bool IsNew { get; } = isNew;
        public List<RedFlag> TextFlags { get; set; } = [];
        public List<RedFlag> Flags { get; set; } = [];
        public bool Passes { get; set; }
    }

    public async Task<PipelineResult> RunAsync(SearchCriteria criteria, Guid? interestId, CancellationToken ct)
    {
        var warnings = new List<string>();
        var homePoint = geo.Resolve(_home.PostalCode);
        if (homePoint is null) warnings.Add($"Heimat-PLZ {_home.PostalCode} unbekannt – Abholkosten werden geschätzt.");

        // ---------- Stufe 1: einsammeln ----------
        var candidates = await CollectAsync(criteria, interestId, warnings, ct);
        var marketMedian = MarketPriceEstimator.Estimate(candidates.Select(c => c.Listing), criteria.Query);

        foreach (var c in candidates)
        {
            c.TextFlags = RedFlagDetector.DetectText(c.Listing, criteria.Query);
            Evaluate(c, marketMedian, homePoint, criteria);
        }

        var prefiltered = candidates.Where(c => c.Passes).OrderByDescending(c => c.Listing.Assessment.PreScore).ToList();

        // ---------- Stufe 2: anreichern ----------
        var toEnrich = prefiltered.Take(_options.EnrichTopK).ToList();
        var enrichedCount = await EnrichAsync(toEnrich, warnings, ct);
        foreach (var c in toEnrich)
        {
            c.TextFlags = RedFlagDetector.DetectText(c.Listing, criteria.Query);
            if (c.Listing.IsDetailLoaded && c.Listing.Assessment.Stage < PipelineStage.Enriched)
                c.Listing.Assessment.Stage = PipelineStage.Enriched;
            Evaluate(c, marketMedian, homePoint, criteria);
        }

        // ---------- Stufe 3: KI ----------
        var (aiCount, aiCached) = criteria.UseAi && ai.IsEnabled
            ? await AssessWithAiAsync(toEnrich, marketMedian, homePoint, criteria, warnings, ct)
            : (0, 0);
        if (criteria.UseAi && !ai.IsEnabled) warnings.Add("Keine KI konfiguriert – Bewertung nur per Heuristik.");

        // ---------- Speichern ----------
        foreach (var c in candidates.Where(c => c.IsNew)) listingRepository.Add(c.Listing);
        await unitOfWork.SaveChangesAsync(ct);

        var results = Sort(candidates.Where(c => c.Passes).Select(c => c.Listing), criteria.Sort).ToList();
        var stats = new PipelineStats(candidates.Count, prefiltered.Count, enrichedCount, aiCount, aiCached,
            candidates.Count(c => c.Listing.Assessment.IsExcluded));

        logger.LogInformation("Pipeline '{Query}': {Collected} gesammelt → {Pre} Vorfilter → {Enriched} angereichert → {Ai} KI ({Cached} Cache)",
            criteria.Query, stats.Collected, stats.PassedPrefilter, stats.Enriched, stats.AiAssessed, stats.AiFromCache);

        return new PipelineResult(results, stats, marketMedian, warnings);
    }

    private async Task<List<Candidate>> CollectAsync(SearchCriteria criteria, Guid? interestId, List<string> warnings, CancellationToken ct)
    {
        var sourceQuery = new SourceQuery
        {
            Query = criteria.Query,
            MinPrice = criteria.MinPrice,
            MaxPrice = criteria.MaxPrice,
            MaxResults = _options.MaxResultsPerSource,
            Conditions = criteria.Conditions,
            OnlyPrivateSellers = criteria.OnlyPrivateSellers,
            NearPostalCode = criteria.Delivery == DeliveryPreference.PickupOnly ? _home.PostalCode : null,
            RadiusKm = criteria.Delivery == DeliveryPreference.PickupOnly
                ? (int)Math.Ceiling(criteria.MaxDistanceKm ?? _home.MaxPickupDistanceKm)
                : null,
        };

        var active = sources.Where(s => criteria.Platforms.Contains(s.Platform)).ToList();
        var searches = await Task.WhenAll(active.Select(async s => (Source: s, Result: await s.SearchAsync(sourceQuery, ct))));

        var candidates = new List<Candidate>();
        foreach (var (source, result) in searches)
        {
            if (!result.IsSuccess)
            {
                warnings.Add($"{source.Platform}: {result.Error}");
                continue;
            }

            var fresh = result.Value!
                .Where(l => l.Price is not null)
                .DistinctBy(l => l.ExternalId)
                .ToList();
            var existing = (await listingRepository.FindExistingAsync(source.Platform, fresh.Select(f => f.ExternalId).ToList(), ct))
                .ToDictionary(l => l.ExternalId);

            foreach (var f in fresh)
            {
                f.InterestId = interestId;
                if (existing.TryGetValue(f.ExternalId, out var known))
                {
                    known.MergeFrom(f);
                    candidates.Add(new Candidate(known, isNew: false));
                }
                else
                {
                    candidates.Add(new Candidate(f, isNew: true));
                }
            }
        }

        return candidates;
    }

    private async Task<int> EnrichAsync(List<Candidate> candidates, List<string> warnings, CancellationToken ct)
    {
        var maxAge = TimeSpan.FromHours(_options.DetailCacheHours);
        var due = candidates
            .Where(c => c.Listing.DetailLoadedAt is null || DateTimeOffset.UtcNow - c.Listing.DetailLoadedAt > maxAge)
            .ToList();
        var bySource = sources.ToDictionary(s => s.Platform);
        using var gate = new SemaphoreSlim(_options.EnrichConcurrency);
        var failures = 0;

        await Task.WhenAll(due.Select(async c =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var result = await bySource[c.Listing.Platform].EnrichAsync(c.Listing, ct);
                if (result.IsSuccess) c.Listing.DetailLoadedAt = DateTimeOffset.UtcNow;
                else Interlocked.Increment(ref failures);
            }
            finally
            {
                gate.Release();
            }
        }));

        if (failures > 0) warnings.Add($"{failures} Detailseite(n) konnten nicht geladen werden.");
        return candidates.Count(c => c.Listing.IsDetailLoaded);
    }

    private async Task<(int Assessed, int Cached)> AssessWithAiAsync(List<Candidate> enriched, decimal? marketMedian,
        GeoPoint? homePoint, SearchCriteria criteria, List<string> warnings, CancellationToken ct)
    {
        var picks = enriched
            .Where(c => c.Passes && !c.Listing.Assessment.IsExcluded && c.Listing.IsDetailLoaded &&
                        c.Listing.Assessment.TrustScore >= _options.MinTrustForAi)
            .OrderByDescending(c => c.Listing.Assessment.PreScore)
            .Take(_options.AiTopK)
            .ToList();

        var cached = 0;
        var todo = new List<Candidate>();
        foreach (var c in picks)
        {
            var a = c.Listing.Assessment;
            if (a.AiVerdict != AiVerdict.NotAssessed && a.AiModel == ai.ModelLabel && a.AiContentHash == AiPrompt.ContentHash(c.Listing))
                cached++;
            else
                todo.Add(c);
        }

        using var gate = new SemaphoreSlim(_options.AiConcurrency);
        var outcomes = await Task.WhenAll(todo.Select(async c =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return (Candidate: c, Result: await ai.AssessAsync(c.Listing, marketMedian, c.Flags, ct));
            }
            finally
            {
                gate.Release();
            }
        }));

        var assessed = 0;
        foreach (var (c, result) in outcomes)
        {
            if (!result.IsSuccess)
            {
                logger.LogWarning("KI-Bewertung fehlgeschlagen für {Id}: {Error}", c.Listing.ExternalId, result.Error);
                continue;
            }

            var r = result.Value!;
            var a = c.Listing.Assessment;
            a.AiLegitimacy = r.Legitimacy;
            a.AiDealQuality = r.DealQuality;
            a.AiEstimatedMarketPrice = r.EstimatedMarketPrice;
            a.AiVerdict = r.Verdict;
            a.AiSummary = r.Summary;
            a.AiModel = ai.ModelLabel;
            a.AiContentHash = AiPrompt.ContentHash(c.Listing);
            a.RedFlags = [.. a.RedFlags.Where(f => !f.StartsWith(AiFlagPrefix, StringComparison.Ordinal)), .. r.RedFlags.Select(f => AiFlagPrefix + f)];
            a.Stage = PipelineStage.AiAssessed;
            usageRepository.Add(new AiUsageEntry { Model = r.Model ?? ai.ModelLabel, InputTokens = r.InputTokens, OutputTokens = r.OutputTokens });
            assessed++;
        }

        if (outcomes.Length > assessed) warnings.Add($"{outcomes.Length - assessed} KI-Bewertung(en) fehlgeschlagen.");

        foreach (var c in picks) Evaluate(c, marketMedian, homePoint, criteria);
        return (assessed, cached);
    }

    /// <summary>Berechnet Kosten, Flags und Scores neu – idempotent, wird nach jeder Stufe aufgerufen.</summary>
    private void Evaluate(Candidate c, decimal? marketMedian, GeoPoint? homePoint, SearchCriteria criteria)
    {
        var l = c.Listing;
        var a = l.Assessment;

        var aiValid = a.AiVerdict != AiVerdict.NotAssessed && a.AiContentHash == AiPrompt.ContentHash(l);
        if (!aiValid && a.AiVerdict != AiVerdict.NotAssessed) ResetAi(a);

        var reference = MarketPriceEstimator.Blend(marketMedian, aiValid ? a.AiEstimatedMarketPrice : null);
        c.Flags = [.. c.TextFlags, .. RedFlagDetector.DetectStructural(l, reference)];

        var cost = costCalculator.Calculate(l, homePoint, geo.Resolve(l.PostalCode), criteria.Delivery, criteria.MaxDistanceKm);
        a.TotalCost = cost.Total;
        a.ChosenDelivery = cost.Mode;
        a.ShippingCost = cost.Shipping;
        a.ShippingCostIsEstimate = cost.ShippingIsEstimate;
        a.BuyerFee = cost.BuyerFee;
        a.PickupTravelCost = cost.PickupTravel;
        a.DistanceKm = cost.DistanceKm;

        a.MarketReferencePrice = reference;
        a.DealScore = Math.Round(DealScorer.DealScore(cost.Total, reference), 1);
        a.TrustScore = Math.Round(TrustScorer.Score(l.Seller, l.Platform), 1);
        a.PreScore = Math.Round(DealScorer.HeuristicScore(a.DealScore, a.TrustScore, c.Flags), 1);
        a.FinalScore = Math.Round(DealScorer.FinalScore(a, c.Flags), 1);
        a.IsExcluded = c.Flags.Exists(f => f.Severity == RedFlagSeverity.Hard);
        a.RedFlags = [.. c.Flags.Select(f => f.Message), .. a.RedFlags.Where(f => f.StartsWith(AiFlagPrefix, StringComparison.Ordinal))];
        a.AssessedAt = DateTimeOffset.UtcNow;

        c.Passes = cost.IsDeliverable && PassesFilters(c, criteria);
    }

    private static void ResetAi(DealAssessment a)
    {
        a.AiLegitimacy = null;
        a.AiDealQuality = null;
        a.AiEstimatedMarketPrice = null;
        a.AiVerdict = AiVerdict.NotAssessed;
        a.AiSummary = null;
        a.AiContentHash = null;
        a.RedFlags = [.. a.RedFlags.Where(f => !f.StartsWith(AiFlagPrefix, StringComparison.Ordinal))];
        if (a.Stage == PipelineStage.AiAssessed) a.Stage = PipelineStage.Enriched;
    }

    private static bool PassesFilters(Candidate c, SearchCriteria criteria)
    {
        var l = c.Listing;
        var a = l.Assessment;

        if (a.IsExcluded && !criteria.IncludeSuspicious) return false;
        if (criteria.MinPrice is { } min && l.Price < min) return false;
        if (criteria.MaxPrice is { } max && l.Price > max) return false;
        if (criteria.MaxTotalCost is { } maxTotal && a.TotalCost > maxTotal) return false;
        if (criteria.Conditions.Count > 0 && l.Condition != ItemCondition.Unknown && !criteria.Conditions.Contains(l.Condition)) return false;
        if (criteria.ExcludeDefective && (l.Condition == ItemCondition.Defective || c.Flags.Exists(f => f.Code == RedFlagCodes.Defective))) return false;

        // Diese Filter greifen erst, wenn wir die Verkäuferdaten wirklich kennen.
        if (l.Seller.IsLoaded)
        {
            if (criteria.OnlyPrivateSellers && l.Seller.IsCommercial) return false;
            if (criteria.MinTrustScore is { } minTrust && a.TrustScore < minTrust) return false;
        }

        return true;
    }

    private static IEnumerable<Listing> Sort(IEnumerable<Listing> items, SortOrder sort) => sort switch
    {
        SortOrder.TotalCostAsc => items.OrderBy(l => l.Assessment.TotalCost),
        SortOrder.Newest => items.OrderByDescending(l => l.PostedAt ?? l.FirstSeenAt),
        SortOrder.MostTrusted => items.OrderByDescending(l => l.Assessment.TrustScore).ThenByDescending(l => l.Assessment.FinalScore),
        _ => items.OrderByDescending(l => l.Assessment.FinalScore).ThenByDescending(l => l.Assessment.Stage),
    };
}
