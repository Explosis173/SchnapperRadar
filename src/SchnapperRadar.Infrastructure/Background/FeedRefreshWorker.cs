using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Application.Services;

namespace SchnapperRadar.Infrastructure.Background;

/// <summary>Füllt den Feed regelmäßig aus deinen Interessen.</summary>
internal sealed class FeedRefreshWorker(
    IServiceScopeFactory scopes,
    FeedRefreshTrigger trigger,
    IOptions<FeedOptions> options,
    ILogger<FeedRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        var interval = TimeSpan.FromMinutes(Math.Max(5, o.RefreshMinutes));
        var nextScheduled = DateTimeOffset.UtcNow + StartupDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Entweder Zeitplan (wenn aktiv) oder manueller Trigger – was zuerst kommt.
            var waitFor = o.BackgroundRefreshEnabled ? nextScheduled - DateTimeOffset.UtcNow : Timeout.InfiniteTimeSpan;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var triggerTask = trigger.WaitAsync(cts.Token);
            var delayTask = waitFor == Timeout.InfiniteTimeSpan
                ? Task.Delay(Timeout.Infinite, cts.Token)
                : Task.Delay(waitFor < TimeSpan.Zero ? TimeSpan.Zero : waitFor, cts.Token);

            await Task.WhenAny(triggerTask, delayTask);
            await cts.CancelAsync();
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<FeedService>().RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Feed-Aktualisierung fehlgeschlagen");
            }

            nextScheduled = DateTimeOffset.UtcNow + interval;
        }
    }
}
