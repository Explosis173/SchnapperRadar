using System.Collections.Concurrent;

namespace SchnapperRadar.Infrastructure.Http;

/// <summary>Höflich scrapen: Mindestabstand zwischen Requests pro Plattform.</summary>
public sealed class HostThrottle
{
    private readonly ConcurrentDictionary<string, (SemaphoreSlim Gate, DateTimeOffset[] Last)> _hosts = new();

    public async Task WaitAsync(string host, TimeSpan minInterval, CancellationToken ct)
    {
        var entry = _hosts.GetOrAdd(host, _ => (new SemaphoreSlim(1, 1), [DateTimeOffset.MinValue]));
        await entry.Gate.WaitAsync(ct);
        try
        {
            var wait = entry.Last[0] + minInterval - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            entry.Last[0] = DateTimeOffset.UtcNow;
        }
        finally
        {
            entry.Gate.Release();
        }
    }
}
