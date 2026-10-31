using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Domain.Entities;

/// <summary>Ein gespeichertes Suchinteresse, aus dem der Feed befüllt wird.</summary>
public sealed class Interest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Query { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public PlatformSet Platforms { get; set; } = PlatformSet.All;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastRunAt { get; set; }
}
