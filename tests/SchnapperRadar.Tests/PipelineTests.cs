using Microsoft.Extensions.Logging.Abstractions;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Ai;
using SchnapperRadar.Application.Common;
using SchnapperRadar.Application.Costs;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Application.Pipeline;
using SchnapperRadar.Application.Search;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Domain.ValueObjects;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SchnapperRadar.Tests;

public class PipelineTests
{
    private sealed class FakeSource(Platform platform, Func<List<Listing>> results) : IListingSource
    {
        public int EnrichCalls;
        public Platform Platform => platform;

        public Task<Result<IReadOnlyList<Listing>>> SearchAsync(SourceQuery query, CancellationToken ct) =>
            Task.FromResult(Result<IReadOnlyList<Listing>>.Success(results()));

        public Task<Result> EnrichAsync(Listing listing, CancellationToken ct)
        {
            Interlocked.Increment(ref EnrichCalls);
            listing.Seller = TestData.GoodVintedSeller();
            listing.Description = listing.Description.Length > 0 ? listing.Description : "Gepflegt, Rechnung vorhanden, Versand versichert.";
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeAi : IAiChatClient
    {
        public int Calls;
        public bool IsConfigured => true;
        public string Provider => "fake";
        public string ModelId => "fake-1";

        public Task<Result<ChatCompletion>> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(Result<ChatCompletion>.Success(new ChatCompletion(
                """{"legitimacy":85,"dealQuality":75,"estimatedMarketPrice":null,"verdict":"buy","redFlags":[],"summary":"ok"}""",
                500, 60, TimeSpan.FromMilliseconds(5))));
        }
    }

    private sealed class FakeGeo : IGeoService
    {
        public GeoPoint? Resolve(string? postalCode) => postalCode is null ? null : new GeoPoint(52.52, 13.40);
    }

    private sealed class MemoryStore : IListingRepository, IAiUsageRepository, IUnitOfWork
    {
        public readonly List<Listing> Listings = [];
        private readonly List<Listing> _pending = [];
        public readonly List<AiUsageEntry> Usage = [];

        public Task<List<Listing>> FindExistingAsync(Platform platform, IReadOnlyCollection<string> externalIds, CancellationToken ct) =>
            Task.FromResult(Listings.Where(l => l.Platform == platform && externalIds.Contains(l.ExternalId)).ToList());
        public Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Listings.Find(l => l.Id == id));
        public Task<List<Listing>> GetFeedAsync(int offset, int limit, CancellationToken ct) => Task.FromResult(Listings.Skip(offset).Take(limit).ToList());
        public Task<List<Listing>> GetSavedAsync(CancellationToken ct) => Task.FromResult(Listings.Where(l => l.IsSaved).ToList());
        public void Add(Listing listing) => _pending.Add(listing);
        public Task<int> DeleteStaleAsync(DateTimeOffset olderThan, CancellationToken ct) => Task.FromResult(0);
        public void Add(AiUsageEntry entry) => Usage.Add(entry);
        public Task<AiUsageSummary> GetSummaryAsync(DateTimeOffset since, CancellationToken ct) => Task.FromResult(new AiUsageSummary(Usage.Count, 0, 0));

        public Task SaveChangesAsync(CancellationToken ct)
        {
            Listings.AddRange(_pending);
            _pending.Clear();
            return Task.CompletedTask;
        }
    }

    /// <summary>40 normale PS5 + 1 Replika + 1 Gesuch + 1 Western-Union-Fake + 1 Schnapper.</summary>
    private static List<Listing> Market() =>
    [
        .. Enumerable.Range(0, 40).Select(i => TestData.Listing($"PS5 Disc Edition #{i}", 380 + i * 3, Platform.Vinted, id: $"n{i}")),
        TestData.Listing("PS5 Replika 1:1", 150, Platform.Vinted, id: "fake1"),
        TestData.Listing("Suche PS5", 300, Platform.Vinted, id: "wanted"),
        TestData.Listing("PS5 neu OVP", 200, Platform.Vinted, description: "Zahlung nur per Western Union", id: "fake2"),
        TestData.Listing("PS5 Disc mit 2 Controllern", 290, Platform.Vinted, id: "deal"),
    ];

    private static (DealPipeline Pipeline, FakeSource Source, FakeAi Ai, MemoryStore Store) Build(PipelineOptions? options = null)
    {
        var source = new FakeSource(Platform.Vinted, Market);
        var ai = new FakeAi();
        var store = new MemoryStore();
        var home = new HomeOptions();
        var pipeline = new DealPipeline([source], new FakeGeo(), new AiDealAssessor(ai), new CostCalculator(home, new CostOptions()),
            store, store, store, MsOptions.Create(home), MsOptions.Create(options ?? new PipelineOptions { EnrichTopK = 10, AiTopK = 3 }),
            NullLogger<DealPipeline>.Instance);
        return (pipeline, source, ai, store);
    }

    [Fact]
    public async Task Funnel_excludes_fakes_and_limits_expensive_stages()
    {
        var (pipeline, source, ai, store) = Build();
        var result = await pipeline.RunAsync(new SearchCriteria { Query = "ps5" }, null, CancellationToken.None);

        Assert.Equal(44, result.Stats.Collected);
        Assert.Equal(3, result.Stats.Excluded);
        Assert.DoesNotContain(result.Items, l => l.ExternalId is "fake1" or "wanted" or "fake2");
        Assert.Equal(10, source.EnrichCalls);   // nur Top-K angereichert
        Assert.Equal(3, ai.Calls);              // nur Top-K an die KI
        Assert.Equal("deal", result.Items[0].ExternalId);
        Assert.Equal(PipelineStage.AiAssessed, result.Items[0].Assessment.Stage);
        Assert.Equal(44, store.Listings.Count);
        Assert.Equal(3, store.Usage.Count);
    }

    [Fact]
    public async Task Second_run_reuses_details_and_ai_cache()
    {
        var (pipeline, source, ai, _) = Build();
        await pipeline.RunAsync(new SearchCriteria { Query = "ps5" }, null, CancellationToken.None);
        var second = await pipeline.RunAsync(new SearchCriteria { Query = "ps5" }, null, CancellationToken.None);

        Assert.Equal(10, source.EnrichCalls);
        Assert.Equal(3, ai.Calls);
        Assert.Equal(3, second.Stats.AiFromCache);
    }

    [Fact]
    public async Task Filters_apply_to_total_cost()
    {
        var (pipeline, _, _, _) = Build();
        var result = await pipeline.RunAsync(new SearchCriteria { Query = "ps5", MaxTotalCost = 400, UseAi = false }, null, CancellationToken.None);

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, l => Assert.True(l.Assessment.TotalCost <= 400));
    }

    [Fact]
    public async Task Suspicious_can_be_shown_on_request()
    {
        var (pipeline, _, _, _) = Build();
        var result = await pipeline.RunAsync(new SearchCriteria { Query = "ps5", IncludeSuspicious = true, UseAi = false }, null, CancellationToken.None);
        Assert.Contains(result.Items, l => l.ExternalId == "fake1" && l.Assessment.IsExcluded);
    }
}
