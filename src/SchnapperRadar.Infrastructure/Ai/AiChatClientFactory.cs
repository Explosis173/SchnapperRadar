using SchnapperRadar.Application.Abstractions;

namespace SchnapperRadar.Infrastructure.Ai;

public sealed record ResolvedAiConfig(string Provider, string Model, string? ApiKey, Uri BaseUrl, bool UsesAnthropicApi,
    bool UsesAzureKeyHeader, string? ReasoningEffort, int MaxOutputTokens);

public static class AiChatClientFactory
{
    public const string HttpClientName = "ai";

    /// <summary>Validiert die Konfiguration. Fehler → KI aus statt Absturz (App läuft dann rein heuristisch).</summary>
    public static (ResolvedAiConfig? Config, string? Error) Resolve(AiOptions o)
    {
        var provider = (o.Provider ?? AiProviders.None).Trim().ToLowerInvariant();
        if (provider is "" or AiProviders.None) return (null, "deaktiviert");

        var d = AiProviders.For(provider);
        var baseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? d.BaseUrl : o.BaseUrl.Trim();
        var model = string.IsNullOrWhiteSpace(o.Model) ? d.Model : o.Model.Trim();

        if (baseUrl is null) return (null, $"Ai__BaseUrl fehlt für Provider '{provider}'");
        if (model is null) return (null, $"Ai__Model fehlt für Provider '{provider}'");
        if (d.NeedsKey && string.IsNullOrWhiteSpace(o.ApiKey)) return (null, $"Ai__ApiKey fehlt für Provider '{provider}'");
        if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute, out var uri))
            return (null, "Ai__BaseUrl ist keine gültige URL");

        // API-Keys nie im Klartext über das Netz – außer an lokale Modelle (Ollama im selben Netz).
        var isLocal = uri.IsLoopback || uri.Host is "ollama" or "host.docker.internal" || uri.Host.EndsWith(".local", StringComparison.Ordinal);
        if (uri.Scheme != Uri.UriSchemeHttps && !isLocal && !string.IsNullOrEmpty(o.ApiKey))
            return (null, "API-Key nur über HTTPS erlaubt");

        return (new ResolvedAiConfig(provider, model, o.ApiKey, uri, d.UsesAnthropicApi, d.UsesAzureKeyHeader,
            o.ReasoningEffort, o.MaxOutputTokens), null);
    }

    public static IAiChatClient Create(HttpClient http, AiOptions options)
    {
        var (config, error) = Resolve(options);
        if (config is null) return new DisabledChatClient(error ?? "deaktiviert");
        http.Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 30) * 4 + 30);
        return config.UsesAnthropicApi
            ? new AnthropicChatClient(http, config)
            : new OpenAiCompatibleChatClient(http, config);
    }
}
