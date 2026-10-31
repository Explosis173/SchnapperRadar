using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Dtos;
using SchnapperRadar.Application.Options;

namespace SchnapperRadar.Application.Services;

public sealed class SystemService(IAiChatClient ai, IAiUsageRepository usage, FeedRefreshLock refreshLock, IOptions<HomeOptions> home, IOptions<FeedOptions> feed)
{
    public async Task<SystemStatusDto> GetStatusAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var day = await usage.GetSummaryAsync(now.AddDays(-1), ct);
        var month = await usage.GetSummaryAsync(now.AddDays(-30), ct);
        return new SystemStatusDto(ai.IsConfigured, ai.Provider, ai.ModelId, home.Value.PostalCode,
            new AiUsageDto(day.Calls, day.InputTokens, day.OutputTokens),
            new AiUsageDto(month.Calls, month.InputTokens, month.OutputTokens),
            feed.Value.BackgroundRefreshEnabled, feed.Value.RefreshMinutes, refreshLock.IsRunning);
    }
}
