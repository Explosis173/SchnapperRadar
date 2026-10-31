using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Infrastructure.Persistence.Repositories;

internal sealed class ListingRepository(AppDbContext db, IOptions<FeedOptions> feed) : IListingRepository
{
    public Task<List<Listing>> FindExistingAsync(Platform platform, IReadOnlyCollection<string> externalIds, CancellationToken ct) =>
        db.Listings.Where(l => l.Platform == platform && externalIds.Contains(l.ExternalId)).ToListAsync(ct);

    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <summary>Feed = nur verifizierte (mind. angereicherte), nicht ausgeschlossene, gut bewertete Inserate aktiver Interessen.</summary>
    public Task<List<Listing>> GetFeedAsync(int offset, int limit, CancellationToken ct)
    {
        var o = feed.Value;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-o.KeepDays);
        return db.Listings.AsNoTracking()
            .Where(l => l.InterestId != null && !l.IsHidden && !l.Assessment.IsExcluded &&
                        l.Assessment.Stage >= PipelineStage.Enriched &&
                        l.Assessment.FinalScore >= o.MinScore &&
                        l.LastSeenAt >= cutoff &&
                        db.Interests.Any(i => i.Id == l.InterestId && i.Enabled))
            .OrderByDescending(l => l.Assessment.FinalScore)
            .ThenByDescending(l => l.LastSeenAt)
            .ThenBy(l => l.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task<List<Listing>> GetSavedAsync(CancellationToken ct) =>
        db.Listings.AsNoTracking().Where(l => l.IsSaved).OrderByDescending(l => l.LastSeenAt).Take(200).ToListAsync(ct);

    public void Add(Listing listing) => db.Listings.Add(listing);

    public Task<int> DeleteStaleAsync(DateTimeOffset olderThan, CancellationToken ct) =>
        db.Listings.Where(l => l.LastSeenAt < olderThan && !l.IsSaved).ExecuteDeleteAsync(ct);
}

internal sealed class InterestRepository(AppDbContext db) : IInterestRepository
{
    public Task<List<Interest>> GetAllAsync(CancellationToken ct) => db.Interests.OrderBy(i => i.CreatedAt).ToListAsync(ct);
    public Task<Interest?> GetByIdAsync(Guid id, CancellationToken ct) => db.Interests.FirstOrDefaultAsync(i => i.Id == id, ct);
    public void Add(Interest interest) => db.Interests.Add(interest);
    public void Remove(Interest interest) => db.Interests.Remove(interest);
}

internal sealed class AiUsageRepository(AppDbContext db) : IAiUsageRepository
{
    public void Add(AiUsageEntry entry) => db.AiUsage.Add(entry);

    public async Task<AiUsageSummary> GetSummaryAsync(DateTimeOffset since, CancellationToken ct)
    {
        var q = db.AiUsage.Where(u => u.CreatedAt >= since);
        var calls = await q.CountAsync(ct);
        if (calls == 0) return new AiUsageSummary(0, 0, 0);
        return new AiUsageSummary(calls, await q.SumAsync(u => (long)u.InputTokens, ct), await q.SumAsync(u => (long)u.OutputTokens, ct));
    }
}
