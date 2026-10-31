namespace SchnapperRadar.Infrastructure.Ai;

/// <summary>
/// Alles per Umgebungsvariable setzbar, z. B.:
///   Ai__Provider=foundry  Ai__BaseUrl=https://&lt;resource&gt;.openai.azure.com/openai/v1/  Ai__Model=gpt-5-nano  Ai__ApiKey=...
/// </summary>
public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>none | foundry | foundry-claude | openai | gemini | groq | github | openrouter | ollama | anthropic | custom</summary>
    public string Provider { get; set; } = AiProviders.None;
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    /// <summary>Nur für Reasoning-Modelle (gpt-5*, o*): minimal | low | medium. Leer lassen für andere.</summary>
    public string? ReasoningEffort { get; set; }
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxOutputTokens { get; set; } = 800;

    /// <summary>
    /// Weitere Anbieter, falls der erste ausfällt oder ins Rate-Limit läuft (z. B. Gratis-Kette Gemini → Groq → GitHub).
    /// Env: Ai__Fallbacks__0__Provider=groq, Ai__Fallbacks__0__ApiKey=...  Einträge ohne Key werden ignoriert.
    /// </summary>
    public List<AiOptions> Fallbacks { get; set; } = [];
}

public static class AiProviders
{
    public const string None = "none";
    public const string Foundry = "foundry";
    public const string FoundryClaude = "foundry-claude";
    public const string OpenAi = "openai";
    public const string Gemini = "gemini";
    public const string Groq = "groq";
    public const string GitHub = "github";
    public const string OpenRouter = "openrouter";
    public const string Ollama = "ollama";
    public const string Anthropic = "anthropic";
    public const string Custom = "custom";

    /// <summary>Ai__Model=auto → bestes verfügbares Modell über /models ermitteln (nur wo der Provider das anbietet).</summary>
    public const string AutoModel = "auto";

    public sealed record Defaults(string? BaseUrl, string? Model, bool NeedsKey, bool UsesAnthropicApi, bool UsesAzureKeyHeader);

    public static Defaults For(string provider) => provider switch
    {
        Foundry => new(null, null, true, false, true),
        FoundryClaude => new(null, null, true, true, true),
        OpenAi => new("https://api.openai.com/v1/", "gpt-5-nano", true, false, false),
        Gemini => new("https://generativelanguage.googleapis.com/v1beta/openai/", AutoModel, true, false, false),
        Groq => new("https://api.groq.com/openai/v1/", AutoModel, true, false, false),
        GitHub => new("https://models.github.ai/inference/", "openai/gpt-4.1-mini", true, false, false),
        OpenRouter => new("https://openrouter.ai/api/v1/", null, true, false, false),
        Ollama => new("http://localhost:11434/v1/", "gemma3:12b", false, false, false),
        Anthropic => new("https://api.anthropic.com/", "claude-haiku-5-5", true, true, false),
        Custom => new(null, null, false, false, false),
        _ => new(null, null, false, false, false),
    };
}
