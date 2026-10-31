using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Common;
using SchnapperRadar.Application.Dtos;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Application.Services;

public sealed class InterestService(IInterestRepository interests, IUnitOfWork unitOfWork)
{
    public const int MaxInterests = 30;

    public async Task<IReadOnlyList<InterestDto>> GetAllAsync(CancellationToken ct) =>
        (await interests.GetAllAsync(ct)).Select(InterestDto.From).ToList();

    public async Task<Result<InterestDto>> CreateAsync(CreateInterestRequest request, CancellationToken ct)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length is < 2 or > SearchService.MaxQueryLength) return Result<InterestDto>.Failure("Suchbegriff muss 2–100 Zeichen lang sein.");
        if (request.MinPrice < 0 || request.MaxPrice < 0 || request.MinPrice > request.MaxPrice) return Result<InterestDto>.Failure("Preisspanne ungültig.");
        if (request.Platforms == PlatformSet.None) return Result<InterestDto>.Failure("Mindestens eine Plattform wählen.");

        var all = await interests.GetAllAsync(ct);
        if (all.Count >= MaxInterests) return Result<InterestDto>.Failure($"Maximal {MaxInterests} Interessen.");

        var interest = new Interest
        {
            Query = query,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            Platforms = request.Platforms & PlatformSet.All,
        };
        interests.Add(interest);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<InterestDto>.Success(InterestDto.From(interest));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var interest = await interests.GetByIdAsync(id, ct);
        if (interest is null) return Result.Failure("Interesse nicht gefunden.");
        interests.Remove(interest);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<InterestDto>> ToggleAsync(Guid id, CancellationToken ct)
    {
        var interest = await interests.GetByIdAsync(id, ct);
        if (interest is null) return Result<InterestDto>.Failure("Interesse nicht gefunden.");
        interest.Enabled = !interest.Enabled;
        await unitOfWork.SaveChangesAsync(ct);
        return Result<InterestDto>.Success(InterestDto.From(interest));
    }
}
