using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Common;
using SchnapperRadar.Application.Dtos;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Application.Pipeline;
using SchnapperRadar.Application.Search;

namespace SchnapperRadar.Application.Services;

/// <summary>Verhindert parallele Feed-Läufe (Hintergrundjob + Button).</summary>
public sealed class FeedRefreshLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public bool IsRunning => Gate.CurrentCount == 0;
}

/// <summary>"Jetzt aktualisieren"-Button → Signal an den Hintergrund-Worker, damit kein HTTP-Request minutenlang blockiert.</summary>
public sealed class FeedRefreshTrigger
{
    private readonly System.Threading.Channels.Channel<bool> _channel =
        System.Threading.Channels.Channel.CreateBounded<bool>(new System.Threading.Channels.BoundedChannelOptions(1)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.DropWrite,
        });

    public void Request() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(CancellationToken ct)
    {
        await _channel.Reader.ReadAsync(ct);
    }
}

public sealed class FeedService(
    DealPipeline pipeline,
    IInterestRepository interests,
    IListingRepository listings,
    IUnitOfWork unitOfWork,
    FeedRefreshLock refreshLock,
    IOptions<FeedOptions> feedOptions,
    ILogger<FeedService> logger)
{
    public const int MaxPageSize = 50;

    public async Task<FeedPageDto> GetPageAsync(int offset, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, MaxPageSize);
        offset = Math.Max(0, offset);
        var items = await listings.GetFeedAsync(offset, limit + 1, ct);
        var page = items.Take(limit).Select(ListingDto.From).ToList();
        return new FeedPageDto(page, offset + page.Count, items.Count > limit);
    }

    public async Task<IReadOnlyList<ListingDto>> GetSavedAsync(CancellationToken ct) =>
        (await listings.GetSavedAsync(ct)).Select(ListingDto.From).ToList();

    /// <returns>false, wenn gerade schon ein Lauf aktiv ist.</returns>
    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        if (!await refreshLock.Gate.WaitAsync(0, ct)) return false;
        try
        {
            foreach (var interest in (await interests.GetAllAsync(ct)).Where(i => i.Enabled))
            {
                try
                {
                    await pipeline.RunAsync(new SearchCriteria
                    {
                        Query = interest.Query,
                        MinPrice = interest.MinPrice,
                        MaxPrice = interest.MaxPrice,
                        Platforms = interest.Platforms,
                    }, interest.Id, ct);
                    interest.LastRunAt = DateTimeOffset.UtcNow;
                    await unitOfWork.SaveChangesAsync(ct);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "Feed-Lauf für '{Query}' fehlgeschlagen", interest.Query);
                }
            }

            var removed = await listings.DeleteStaleAsync(DateTimeOffset.UtcNow.AddDays(-feedOptions.Value.KeepDays), ct);
            if (removed > 0) logger.LogInformation("{Count} alte Inserate entfernt", removed);
            return true;
        }
        finally
        {
            refreshLock.Gate.Release();
        }
    }

    public async Task<Result> HideAsync(Guid id, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(id, ct);
        if (listing is null) return Result.Failure("Inserat nicht gefunden.");
        listing.Hide();
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<bool>> ToggleSavedAsync(Guid id, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(id, ct);
        if (listing is null) return Result<bool>.Failure("Inserat nicht gefunden.");
        listing.ToggleSaved();
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(listing.IsSaved);
    }
}
