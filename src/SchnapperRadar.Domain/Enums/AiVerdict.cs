namespace SchnapperRadar.Domain.Enums;

public enum AiVerdict
{
    NotAssessed = 0,
    Buy = 1,
    Check = 2,
    Avoid = 3,
}

/// <summary>Wie weit ein Inserat durch den Trichter gekommen ist.</summary>
public enum PipelineStage
{
    Collected = 0,
    Enriched = 1,
    AiAssessed = 2,
}

public enum RedFlagSeverity
{
    Minor = 1,
    Major = 2,
    /// <summary>Fliegt sofort raus (z. B. "Replika", "Western Union").</summary>
    Hard = 3,
}
