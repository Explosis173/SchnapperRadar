using SchnapperRadar.Application.Common;
using SchnapperRadar.Application.Dtos;
using SchnapperRadar.Application.Pipeline;
using SchnapperRadar.Application.Search;

namespace SchnapperRadar.Application.Services;

public sealed class SearchService(DealPipeline pipeline)
{
    public const int MaxQueryLength = 100;

    public async Task<Result<SearchResponseDto>> SearchAsync(SearchCriteria criteria, CancellationToken ct)
    {
        var validation = Validate(criteria);
        if (!validation.IsSuccess) return Result<SearchResponseDto>.Failure(validation.Error!);

        var result = await pipeline.RunAsync(criteria with { Query = criteria.Query.Trim() }, interestId: null, ct);
        return Result<SearchResponseDto>.Success(new SearchResponseDto(
            result.Items.Select(ListingDto.From).ToList(), result.Stats, result.MarketReference, result.Warnings));
    }

    public static Result Validate(SearchCriteria c)
    {
        var q = c.Query?.Trim() ?? string.Empty;
        if (q.Length is < 2 or > MaxQueryLength) return Result.Failure($"Suchbegriff muss 2–{MaxQueryLength} Zeichen lang sein.");
        if (c.Platforms == Domain.Enums.PlatformSet.None) return Result.Failure("Mindestens eine Plattform wählen.");
        if (c.MinPrice < 0 || c.MaxPrice < 0 || c.MaxTotalCost < 0) return Result.Failure("Preise dürfen nicht negativ sein.");
        if (c.MinPrice > c.MaxPrice) return Result.Failure("Mindestpreis ist größer als Höchstpreis.");
        if (c.MaxDistanceKm is < 0 or > 1000) return Result.Failure("Entfernung muss zwischen 0 und 1000 km liegen.");
        if (c.MinTrustScore is < 0 or > 100) return Result.Failure("Mindest-Vertrauen muss 0–100 sein.");
        return Result.Success();
    }
}
