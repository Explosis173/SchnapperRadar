using System.Collections.Concurrent;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Common;

namespace SchnapperRadar.Infrastructure.Ai;

/// <summary>
/// Probiert mehrere Anbieter der Reihe nach. Wer ins Rate-Limit läuft oder ausfällt, wird kurz pausiert –
/// so lassen sich mehrere Gratis-Kontingente (Gemini, Groq, GitHub Models) kombinieren.
/// </summary>
internal sealed class FallbackChatClient(IReadOnlyList<IAiChatClient> clients) : IAiChatClient
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    // Prozessweit: ein im Rate-Limit hängender Anbieter wird auch von parallelen Requests übersprungen.
    private static readonly ConcurrentDictionary<string, DateTimeOffset> PausedUntil = new();

    public bool IsConfigured => true;
    public string Provider => string.Join(" → ", clients.Select(c => c.Provider));
    public string ModelId => clients[0].ModelId + (clients.Count > 1 ? $" (+{clients.Count - 1} Fallback)" : string.Empty);

    public async Task<Result<ChatCompletion>> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var errors = new List<string>();
        var now = DateTimeOffset.UtcNow;
        var available = clients.Where(c => !PausedUntil.TryGetValue(Key(c), out var until) || until <= now).ToList();
        // Alle pausiert? Dann trotzdem den ersten versuchen statt gar nichts zu tun.
        if (available.Count == 0) available = [clients[0]];

        foreach (var client in available)
        {
            var result = await client.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            if (result.IsSuccess)
            {
                PausedUntil.TryRemove(Key(client), out _);
                return result;
            }

            errors.Add($"{client.Provider}: {result.Error}");
            if (AiErrors.IsTransient(result.Error)) PausedUntil[Key(client)] = DateTimeOffset.UtcNow + Cooldown;
        }

        return Result<ChatCompletion>.Failure(string.Join(" | ", errors));
    }

    private static string Key(IAiChatClient c) => $"{c.Provider}/{c.ModelId}";

    internal static void ResetForTests() => PausedUntil.Clear();
}
