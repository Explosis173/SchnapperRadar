using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Common;

namespace SchnapperRadar.Infrastructure.Ai;

internal sealed class DisabledChatClient(string reason) : IAiChatClient
{
    public bool IsConfigured => false;
    public string Provider => AiProviders.None;
    public string ModelId => reason;

    public Task<Result<ChatCompletion>> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct) =>
        Task.FromResult(Result<ChatCompletion>.Failure("KI ist nicht konfiguriert."));
}

/// <summary>OpenAI-kompatibel: Microsoft Foundry (Azure OpenAI v1), OpenAI, Gemini, Groq, GitHub Models, OpenRouter, Ollama.</summary>
internal sealed class OpenAiCompatibleChatClient(HttpClient http, ResolvedAiConfig config) : IAiChatClient
{
    private bool _jsonModeUnsupported;

    public bool IsConfigured => true;
    public string Provider => config.Provider;
    public string ModelId => ModelResolver.Cached(config) ?? config.Model;

    public Task<Result<ChatCompletion>> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct) =>
        AiHttp.Guard(() => CompleteCoreAsync(systemPrompt, userPrompt, ct), ct);

    private async Task<Result<ChatCompletion>> CompleteCoreAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var model = await ModelResolver.ResolveAsync(http, config, ct);
        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt },
            },
        };
        if (!_jsonModeUnsupported) body["response_format"] = new JsonObject { ["type"] = "json_object" };
        if (!string.IsNullOrWhiteSpace(config.ReasoningEffort)) body["reasoning_effort"] = config.ReasoningEffort;

        using var response = await SendAsync(body, ct);

        // Manche Modelle/Provider kennen keinen JSON-Mode → einmal ohne probieren, Parser ist tolerant.
        if (response.StatusCode == HttpStatusCode.BadRequest && !_jsonModeUnsupported)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            if (error.Contains("response_format", StringComparison.OrdinalIgnoreCase) || error.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                _jsonModeUnsupported = true;
                return await CompleteCoreAsync(systemPrompt, userPrompt, ct);
            }
            return Result<ChatCompletion>.Failure($"KI-Fehler 400: {Truncate(error)}");
        }

        if (!response.IsSuccessStatusCode)
            return Result<ChatCompletion>.Failure($"KI-Fehler {(int)response.StatusCode}: {Truncate(await response.Content.ReadAsStringAsync(ct))}");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var content = root.GetProperty("choices")[0].GetProperty("message").TryGetProperty("content", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(content)) return Result<ChatCompletion>.Failure("Leere KI-Antwort.");

        var (input, output) = root.TryGetProperty("usage", out var u)
            ? (Int(u, "prompt_tokens"), Int(u, "completion_tokens"))
            : (0, 0);
        return Result<ChatCompletion>.Success(new ChatCompletion(content, input, output, sw.Elapsed, $"{config.Provider}/{model}"));
    }

    private async Task<HttpResponseMessage> SendAsync(JsonObject body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(config.BaseUrl, "chat/completions"))
        {
            Content = Json(body),
        };
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            if (config.UsesAzureKeyHeader) request.Headers.Add("api-key", config.ApiKey);
            else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
        return await http.SendAsync(request, ct);
    }

    private static StringContent Json(JsonObject body) => new(body.ToJsonString(), Encoding.UTF8, "application/json");

    private static int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;
    private static string Truncate(string s) => s.Length > 300 ? s[..300] : s;
}

/// <summary>Anthropic Messages API – direkt oder Claude über Microsoft Foundry.</summary>
internal sealed class AnthropicChatClient(HttpClient http, ResolvedAiConfig config) : IAiChatClient
{
    private const string ApiVersion = "2023-06-01";

    public bool IsConfigured => true;
    public string Provider => config.Provider;
    public string ModelId => config.Model;

    public Task<Result<ChatCompletion>> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct) =>
        AiHttp.Guard(() => CompleteCoreAsync(systemPrompt, userPrompt, ct), ct);

    private async Task<Result<ChatCompletion>> CompleteCoreAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var body = new JsonObject
        {
            ["model"] = config.Model,
            ["max_tokens"] = config.MaxOutputTokens,
            ["system"] = systemPrompt,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = userPrompt },
                // Prefill erzwingt, dass die Antwort direkt mit JSON beginnt.
                new JsonObject { ["role"] = "assistant", ["content"] = "{" },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(config.BaseUrl, "v1/messages")) { Content = Json(body) };
        request.Headers.Add("x-api-key", config.ApiKey);
        if (config.UsesAzureKeyHeader) request.Headers.Add("api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);

        using var response = await http.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        // Neuere Modelle erlauben teils kein Prefill → ohne wiederholen.
        if (response.StatusCode == HttpStatusCode.BadRequest && raw.Contains("prefill", StringComparison.OrdinalIgnoreCase))
            return await WithoutPrefillAsync(body, sw, ct);

        if (!response.IsSuccessStatusCode)
            return Result<ChatCompletion>.Failure($"KI-Fehler {(int)response.StatusCode}: {(raw.Length > 300 ? raw[..300] : raw)}");

        return Parse(raw, prefix: "{", sw);
    }

    private async Task<Result<ChatCompletion>> WithoutPrefillAsync(JsonObject body, Stopwatch sw, CancellationToken ct)
    {
        ((JsonArray)body["messages"]!).RemoveAt(1);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(config.BaseUrl, "v1/messages")) { Content = Json(body) };
        request.Headers.Add("x-api-key", config.ApiKey);
        if (config.UsesAzureKeyHeader) request.Headers.Add("api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        using var response = await http.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode
            ? Parse(raw, prefix: string.Empty, sw)
            : Result<ChatCompletion>.Failure($"KI-Fehler {(int)response.StatusCode}: {(raw.Length > 300 ? raw[..300] : raw)}");
    }

    private static StringContent Json(JsonObject body) => new(body.ToJsonString(), Encoding.UTF8, "application/json");

    private Result<ChatCompletion> Parse(string raw, string prefix, Stopwatch sw)
    {
        using var doc = JsonDocument.Parse(raw);
        var text = string.Concat(doc.RootElement.GetProperty("content").EnumerateArray()
            .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "text")
            .Select(b => b.GetProperty("text").GetString()));
        var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : default;
        var input = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("input_tokens", out var i) ? i.GetInt32() : 0;
        var output = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : 0;
        return Result<ChatCompletion>.Success(new ChatCompletion(prefix + text, input, output, sw.Elapsed, $"{config.Provider}/{config.Model}"));
    }
}

internal static class AiErrors
{
    /// <summary>Lohnt es sich, einen anderen Anbieter zu probieren bzw. diesen kurz zu pausieren?</summary>
    public static bool IsTransient(string? error) =>
        error is not null &&
        (error.StartsWith("KI-Fehler 429", StringComparison.Ordinal) ||
         error.StartsWith("KI-Fehler 5", StringComparison.Ordinal) ||
         error.StartsWith("KI-Fehler 403", StringComparison.Ordinal) ||
         error.StartsWith("KI nicht erreichbar", StringComparison.Ordinal) ||
         error.StartsWith("KI-Timeout", StringComparison.Ordinal) ||
         error.StartsWith("KI vorübergehend", StringComparison.Ordinal));
}

/// <summary>Netzwerk-/Timeout-/Formatfehler → Result.Failure statt Exception, damit eine kaputte KI nie die Suche abbricht.</summary>
internal static class AiHttp
{
    public static async Task<Result<ChatCompletion>> Guard(Func<Task<Result<ChatCompletion>>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException ex)
        {
            return Result<ChatCompletion>.Failure($"KI nicht erreichbar: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<ChatCompletion>.Failure("KI-Timeout.");
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            return Result<ChatCompletion>.Failure("KI-Timeout.");
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            return Result<ChatCompletion>.Failure("KI vorübergehend gesperrt (zu viele Fehler).");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return Result<ChatCompletion>.Failure($"KI-Antwort unlesbar: {ex.Message}");
        }
    }
}
