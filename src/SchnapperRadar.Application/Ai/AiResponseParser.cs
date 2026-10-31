using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SchnapperRadar.Application.Common;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Application.Ai;

/// <summary>Tolerantes Parsing – kleine lokale Modelle halten sich nicht immer exakt ans Format.</summary>
public static partial class AiResponseParser
{
    private const int MaxFlags = 4;
    private const int MaxSummaryChars = 200;

    public sealed record Parsed(double Legitimacy, double DealQuality, decimal? EstimatedMarketPrice, AiVerdict Verdict,
        IReadOnlyList<string> RedFlags, string Summary);

    public static Result<Parsed> Parse(string raw)
    {
        var text = ThinkRegex().Replace(raw, string.Empty);
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return Result<Parsed>.Failure("KI-Antwort enthält kein JSON.");

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var root = doc.RootElement;

            var legitimacy = ReadNumber(root, "legitimacy");
            var dealQuality = ReadNumber(root, "dealQuality");
            if (legitimacy is null || dealQuality is null)
                return Result<Parsed>.Failure("KI-Antwort ohne Scores.");

            var market = ReadNumber(root, "estimatedMarketPrice");
            var verdict = ReadString(root, "verdict")?.Trim().ToLowerInvariant() switch
            {
                "buy" or "kaufen" => AiVerdict.Buy,
                "avoid" or "meiden" => AiVerdict.Avoid,
                _ => AiVerdict.Check,
            };

            var flags = root.TryGetProperty("redFlags", out var f) && f.ValueKind == JsonValueKind.Array
                ? f.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => Truncate(e.GetString()!, 80)).Take(MaxFlags).ToList()
                : [];

            return Result<Parsed>.Success(new Parsed(
                Math.Clamp(legitimacy.Value, 0, 100),
                Math.Clamp(dealQuality.Value, 0, 100),
                market is > 0 and < 1_000_000 ? Math.Round((decimal)market.Value, 2) : null,
                verdict,
                flags,
                Truncate(ReadString(root, "summary") ?? string.Empty, MaxSummaryChars)));
        }
        catch (JsonException ex)
        {
            return Result<Parsed>.Failure($"KI-JSON ungültig: {ex.Message}");
        }
    }

    private static double? ReadNumber(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var e)) return null;
        return e.ValueKind switch
        {
            JsonValueKind.Number => e.GetDouble(),
            JsonValueKind.String when double.TryParse(e.GetString()?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex ThinkRegex();
}
