using SchnapperRadar.Application.Common;

namespace SchnapperRadar.Application.Abstractions;

public sealed record ChatCompletion(string Content, int InputTokens, int OutputTokens, TimeSpan Latency);

/// <summary>Provider-neutraler LLM-Zugang (Foundry, OpenAI, Gemini, Ollama, Anthropic, ...).</summary>
public interface IAiChatClient
{
    bool IsConfigured { get; }
    string Provider { get; }
    string ModelId { get; }
    Task<Result<ChatCompletion>> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}
