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
        var (primary, error) = Resolve(options);
        var configs = new List<(ResolvedAiConfig Config, AiOptions Options)>();
        if (primary is not null) configs.Add((primary, options));
        foreach (var fb in options.Fallbacks)
        {
            // Fallbacks ohne Key/URL einfach überspringen – so kann .env alle Gratis-Anbieter vorbereitet enthalten.
            if (Resolve(fb).Config is { } cfg) configs.Add((cfg, fb));
        }

        if (configs.Count == 0) return new DisabledChatClient(error ?? "deaktiviert");

        var timeout = configs.Max(c => Math.Max(c.Options.TimeoutSeconds, 30));
        http.Timeout = TimeSpan.FromSeconds(timeout * 4 + 30);

        var clients = configs.Select(c => c.Config.UsesAnthropicApi
            ? (IAiChatClient)new AnthropicChatClient(http, c.Config)
            : new OpenAiCompatibleChatClient(http, c.Config)).ToList();
        return clients.Count == 1 ? clients[0] : new FallbackChatClient(clients);
    }
}
