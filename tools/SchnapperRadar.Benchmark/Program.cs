// Modell-Benchmark: Welches LLM erkennt Fakes am zuverlässigsten – und was kostet es?
//
//   dotnet run --project tools/SchnapperRadar.Benchmark [-- --models models.json --dataset dataset.json --only "gpt-5"]
//
// Modelle ohne gesetzte API-Key-Umgebungsvariable werden übersprungen.
using System.Globalization;
using System.Text;
using System.Text.Json;
using SchnapperRadar.Application.Ai;
using SchnapperRadar.Application.Scoring;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Infrastructure.Ai;

var argMap = ParseArgs(args);
var baseDir = AppContext.BaseDirectory;
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
var models = JsonSerializer.Deserialize<List<ModelSpec>>(File.ReadAllText(argMap.GetValueOrDefault("models", Path.Combine(baseDir, "models.json"))), jsonOptions)!;
var cases = JsonSerializer.Deserialize<List<BenchCase>>(File.ReadAllText(argMap.GetValueOrDefault("dataset", Path.Combine(baseDir, "dataset.json"))), jsonOptions)!;
var only = argMap.GetValueOrDefault("only");

var results = new List<ModelResult>();

foreach (var spec in models.Where(m => only is null || m.Name.Contains(only, StringComparison.OrdinalIgnoreCase)))
{
    var apiKey = spec.ApiKeyEnv is null ? null : Environment.GetEnvironmentVariable(spec.ApiKeyEnv);
    var baseUrl = spec.BaseUrlEnv is not null ? Environment.GetEnvironmentVariable(spec.BaseUrlEnv) : spec.BaseUrl;
    var options = new AiOptions { Provider = spec.Provider, Model = spec.Model, ApiKey = apiKey, BaseUrl = baseUrl, ReasoningEffort = spec.ReasoningEffort };
    var (config, error) = AiChatClientFactory.Resolve(options);
    if (config is null)
    {
        Console.WriteLine($"⏭  {spec.Name}: übersprungen ({error})");
        continue;
    }

    Console.WriteLine($"▶  {spec.Name} …");
    using var http = new HttpClient();
    var assessor = new AiDealAssessor(AiChatClientFactory.Create(http, options));
    var r = new ModelResult(spec);

    foreach (var c in cases)
    {
        var listing = c.ToListing();
        var flags = RedFlagDetector.DetectText(listing, c.Title).Concat(RedFlagDetector.DetectStructural(listing, c.MarketMedian)).ToList();
        listing.Assessment.TotalCost = c.Price;

        var outcome = await assessor.AssessAsync(listing, c.MarketMedian, flags, CancellationToken.None);
        if (!outcome.IsSuccess)
        {
            r.RecordFailure(c);
            Console.WriteLine($"   ✗ {c.Id}: {outcome.Error}");
            continue;
        }

        var a = outcome.Value!;
        var verdict = a.Verdict.ToString().ToLowerInvariant();
        var ok = c.Acceptable.Contains(verdict);
        r.Record(c, a, ok, verdict);
        Console.WriteLine($"   {(ok ? "✓" : "✗")} {c.Id,-24} → {verdict,-6} legit {a.Legitimacy,3:0} deal {a.DealQuality,3:0}  {a.Latency.TotalSeconds:0.0}s");
    }
    results.Add(r);
}

if (results.Count == 0)
{
    Console.WriteLine("\nKein Modell konfiguriert. Setz z. B. FOUNDRY_BASE_URL + FOUNDRY_API_KEY, GEMINI_API_KEY oder starte Ollama.");
    return 1;
}

var report = BuildReport(results, cases.Count);
Console.WriteLine();
Console.WriteLine(report);
var reportPath = Path.Combine(Environment.CurrentDirectory, "benchmark-results.md");
File.WriteAllText(reportPath, report);
Console.WriteLine($"Report gespeichert: {reportPath}");
return 0;

static string BuildReport(List<ModelResult> results, int caseCount)
{
    var ranked = results.OrderByDescending(r => r.Quality).ThenBy(r => r.CostPer1000 ?? double.MaxValue).ToList();
    var sb = new StringBuilder();
    sb.AppendLine(CultureInfo.InvariantCulture, $"# Modell-Benchmark ({caseCount} Testinserate, {DateTime.Now:yyyy-MM-dd HH:mm})");
    sb.AppendLine();
    sb.AppendLine("| Modell | Qualität | Fakes erkannt | Fehlalarme | Urteil korrekt | Fehler | Ø Latenz | Tokens/Inserat | USD pro 1000 Prüfungen* |");
    sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
    foreach (var r in ranked)
    {
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| {r.Spec.Name} | **{r.Quality:0}** | {r.ScamRecall:P0} | {r.FalseAlarmRate:P0} | {r.Accuracy:P0} | {r.Failures} | {r.AvgLatency:0.0} s | {r.AvgTokens:0} | {(r.CostPer1000 is { } c ? c.ToString("0.000", CultureInfo.InvariantCulture) : "?")} |");
    }

    var best = ranked[0];
    var value = ranked.Where(r => r.Quality >= best.Quality - 5 && r.CostPer1000 is not null).OrderBy(r => r.CostPer1000).FirstOrDefault();
    sb.AppendLine();
    sb.AppendLine(CultureInfo.InvariantCulture, $"**Bestes Modell:** {best.Spec.Name} (Qualität {best.Quality:0})");
    if (value is not null && value != best)
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Bestes Preis-Leistungs-Verhältnis:** {value.Spec.Name} – max. 5 Punkte schlechter, aber {value.CostPer1000:0.000} statt {best.CostPer1000:0.000} USD/1000");
    sb.AppendLine();
    sb.AppendLine("Qualität = 50 % Fake-Erkennung + 30 % korrektes Urteil + 20 % keine Fehlalarme (Parse-Fehler zählen als falsch).");
    sb.AppendLine("*Kosten aus den Preisen in models.json (USD pro 1 Mio. Tokens) – bitte mit deinem Azure-Portal abgleichen.");
    return sb.ToString();
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>();
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i].StartsWith("--", StringComparison.Ordinal)) map[args[i][2..]] = args[++i];
    return map;
}

internal sealed record ModelSpec(string Name, string Provider, string Model, string? BaseUrl, string? BaseUrlEnv, string? ApiKeyEnv,
    string? ReasoningEffort, double? InputPricePerMTokens, double? OutputPricePerMTokens);

internal sealed record SellerSpec(double? RatingPercent, int? RatingCount, List<string>? Badges, int? MemberSinceDaysAgo, int? SoldItems, bool IsCommercial);

internal sealed record BenchCase(string Id, Platform Platform, string Title, string Description, decimal Price, decimal MarketMedian, int Images,
    ItemCondition Condition, SellerSpec Seller, bool IsScam, List<string> Acceptable)
{
    public Listing ToListing() => new()
    {
        Platform = Platform,
        ExternalId = Id,
        Url = "https://www.kleinanzeigen.de/s-anzeige/bench/1",
        Title = Title,
        Description = Description,
        Price = Price,
        ImageCount = Images,
        Condition = Condition,
        ShippingAvailable = true,
        DetailLoadedAt = DateTimeOffset.UtcNow,
        Seller = new SellerInfo
        {
            IsLoaded = true,
            RatingPercent = Seller.RatingPercent,
            RatingCount = Seller.RatingCount,
            Badges = Seller.Badges ?? [],
            SoldItems = Seller.SoldItems,
            IsCommercial = Seller.IsCommercial,
            MemberSince = Seller.MemberSinceDaysAgo is { } d ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-d)) : null,
        },
    };
}

internal sealed class ModelResult(ModelSpec spec)
{
    private int _scams, _scamsCaught, _clearlyGood, _falseAlarms, _correct, _total;
    private long _tokens;
    private double _inputTokens, _outputTokens, _latency;

    public ModelSpec Spec { get; } = spec;
    public int Failures { get; private set; }

    /// <summary>Kaputte Antwort = Fake nicht erkannt bzw. Urteil falsch.</summary>
    public void RecordFailure(BenchCase c)
    {
        Failures++;
        if (c.IsScam) _scams++;
    }

    public void Record(BenchCase c, AiAssessment a, bool correct, string verdict)
    {
        _total++;
        if (correct) _correct++;
        if (c.IsScam)
        {
            _scams++;
            if (verdict == "avoid") _scamsCaught++;
        }
        else if (!c.Acceptable.Contains("avoid"))
        {
            _clearlyGood++;
            if (verdict == "avoid") _falseAlarms++;
        }
        _inputTokens += a.InputTokens;
        _outputTokens += a.OutputTokens;
        _tokens += a.InputTokens + a.OutputTokens;
        _latency += a.Latency.TotalSeconds;
    }

    private int Attempts => _total + Failures;
    public double ScamRecall => _scams == 0 ? 0 : (double)_scamsCaught / _scams;
    public double FalseAlarmRate => _clearlyGood == 0 ? 0 : (double)_falseAlarms / _clearlyGood;
    public double Accuracy => Attempts == 0 ? 0 : (double)_correct / Attempts;
    public double Quality => 100 * (0.5 * ScamRecall + 0.3 * Accuracy + 0.2 * (1 - FalseAlarmRate));
    public double AvgLatency => _total == 0 ? 0 : _latency / _total;
    public double AvgTokens => _total == 0 ? 0 : (double)_tokens / _total;

    public double? CostPer1000 => Spec.InputPricePerMTokens is { } pin && Spec.OutputPricePerMTokens is { } pout && _total > 0
        ? (_inputTokens * pin + _outputTokens * pout) / 1_000_000 / _total * 1000
        : null;
}
