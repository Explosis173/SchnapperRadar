using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Application.Abstractions;

public interface IListingRepository
{
    Task<List<Listing>> FindExistingAsync(Platform platform, IReadOnlyCollection<string> externalIds, CancellationToken ct);
    Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Listing>> GetFeedAsync(int offset, int limit, CancellationToken ct);
    Task<List<Listing>> GetSavedAsync(CancellationToken ct);
    void Add(Listing listing);
    Task<int> DeleteStaleAsync(DateTimeOffset olderThan, CancellationToken ct);
}

public interface IInterestRepository
{
    Task<List<Interest>> GetAllAsync(CancellationToken ct);
    Task<Interest?> GetByIdAsync(Guid id, CancellationToken ct);
    void Add(Interest interest);
    void Remove(Interest interest);
}

public interface IAiUsageRepository
{
    void Add(AiUsageEntry entry);
    Task<AiUsageSummary> GetSummaryAsync(DateTimeOffset since, CancellationToken ct);
}

public sealed record AiUsageSummary(int Calls, long InputTokens, long OutputTokens);

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
