using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Common;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.ValueObjects;

namespace SchnapperRadar.Application.Ai;

public sealed class AiDealAssessor(IAiChatClient client)
{
    public bool IsEnabled => client.IsConfigured;
    public string ModelLabel => $"{client.Provider}/{client.ModelId}";

    public async Task<Result<AiAssessment>> AssessAsync(Listing listing, decimal? marketMedian,
        IReadOnlyCollection<RedFlag> heuristicFlags, CancellationToken ct)
    {
        var completion = await client.CompleteJsonAsync(AiPrompt.System, AiPrompt.BuildUser(listing, marketMedian, heuristicFlags), ct);
        if (!completion.IsSuccess) return Result<AiAssessment>.Failure(completion.Error!);

        var c = completion.Value!;
        var parsed = AiResponseParser.Parse(c.Content);
        if (!parsed.IsSuccess) return Result<AiAssessment>.Failure(parsed.Error!);

        var p = parsed.Value!;
        return Result<AiAssessment>.Success(new AiAssessment(p.Legitimacy, p.DealQuality, p.EstimatedMarketPrice, p.Verdict,
            p.RedFlags, p.Summary, c.InputTokens, c.OutputTokens, c.Latency));
    }
}
