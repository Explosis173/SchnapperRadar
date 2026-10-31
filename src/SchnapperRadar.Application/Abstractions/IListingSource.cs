using SchnapperRadar.Application.Common;
using SchnapperRadar.Application.Search;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Application.Abstractions;

/// <summary>Eine Marktplatz-Quelle (Kleinanzeigen, Vinted, ...).</summary>
public interface IListingSource
{
    Platform Platform { get; }

    /// <summary>Günstige Stufe: nur Suchergebnisliste laden.</summary>
    Task<Result<IReadOnlyList<Listing>>> SearchAsync(SourceQuery query, CancellationToken ct);

    /// <summary>Teure Stufe: Detailseite + Verkäuferprofil nachladen. Wird nur für vielversprechende Kandidaten aufgerufen.</summary>
    Task<Result> EnrichAsync(Listing listing, CancellationToken ct);
}
