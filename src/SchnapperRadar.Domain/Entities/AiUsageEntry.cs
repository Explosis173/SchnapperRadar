namespace SchnapperRadar.Domain.Entities;

public sealed class AiUsageEntry
{
    public long Id { get; set; }
    public required string Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
