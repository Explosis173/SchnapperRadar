using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SchnapperRadar.Infrastructure.Ai;

/// <summary>
/// Ai__Model=auto: fragt die Modellliste des Anbieters ab und nimmt das neueste passende (günstige) Modell.
/// Gratis-Anbieter benennen ihre Modelle ständig um – so muss man nichts nachpflegen.
/// </summary>
internal static partial class ModelResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
    private static readonly ConcurrentDictionary<string, (string Model, DateTimeOffset At)> Cache = new();

    /// <summary>Bevorzugte Muster pro Anbieter (erste Gruppe = Versionsnummer, höchste gewinnt).</summary>
    private static readonly Dictionary<string, (Regex[] Preferences, string Fallback)> Rules = new()
    {
        [AiProviders.Gemini] = (
            [GeminiFlashLite(), GeminiFlash()],
            "gemini-flash-lite-latest"),
        [AiProviders.Groq] = (
            [GroqGptOss120(), GroqLlama70(), GroqGptOss20()],
            "llama-3.3-70b-versatile"),
    };

    public static string? Cached(ResolvedAiConfig config) =>
        Cache.TryGetValue(config.BaseUrl.ToString(), out var e) ? e.Model : null;

    public static async Task<string> ResolveAsync(HttpClient http, ResolvedAiConfig config, CancellationToken ct)
    {
        if (!string.Equals(config.Model, AiProviders.AutoModel, StringComparison.OrdinalIgnoreCase)) return config.Model;

        var key = config.BaseUrl.ToString();
        if (Cache.TryGetValue(key, out var hit) && DateTimeOffset.UtcNow - hit.At < CacheDuration) return hit.Model;

        var rules = Rules.TryGetValue(config.Provider, out var r) ? r : ([], config.Model);
        var model = rules.Fallback;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(config.BaseUrl, "models"));
            if (!string.IsNullOrEmpty(config.ApiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var ids = doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                    ? data.EnumerateArray()
                        .Select(m => m.TryGetProperty("id", out var id) ? id.GetString() : null)
                        .Where(id => id is not null)
                        .Select(id => id!.StartsWith("models/", StringComparison.Ordinal) ? id[7..] : id)
                        .ToList()
                    : [];
                model = Pick(ids, rules.Preferences) ?? model;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Modellliste nicht erreichbar → Alias-Fallback; der eigentliche Call meldet dann ggf. den echten Fehler.
        }

        Cache[key] = (model, DateTimeOffset.UtcNow);
        return model;
    }

    internal static string? Pick(IReadOnlyCollection<string> ids, IReadOnlyList<Regex> preferences)
    {
        foreach (var pattern in preferences)
        {
            var best = ids
                .Select(id => (Id: id, Match: pattern.Match(id)))
                .Where(x => x.Match.Success)
                .OrderByDescending(x => ParseVersion(x.Match.Groups.Count > 1 ? x.Match.Groups[1].Value : "0"))
                .Select(x => x.Id)
                .FirstOrDefault();
            if (best is not null) return best;
        }
        return null;
    }

    private static Version ParseVersion(string v) =>
        Version.TryParse(v.Contains('.', StringComparison.Ordinal) ? v : v + ".0", out var parsed) ? parsed : new Version(0, 0);

    // Nur stabile Modelle: keine preview/exp/tts/image/live-Varianten (die Muster sind am Ende verankert).
    [GeneratedRegex(@"^gemini-(\d+(?:\.\d+)?)-flash-lite$")]
    private static partial Regex GeminiFlashLite();

    [GeneratedRegex(@"^gemini-(\d+(?:\.\d+)?)-flash$")]
    private static partial Regex GeminiFlash();

    [GeneratedRegex(@"^openai/gpt-oss-(120)b$")]
    private static partial Regex GroqGptOss120();

    [GeneratedRegex(@"^llama-(\d+(?:\.\d+)?)-70b-versatile$")]
    private static partial Regex GroqLlama70();

    [GeneratedRegex(@"^openai/gpt-oss-(20)b$")]
    private static partial Regex GroqGptOss20();
}
