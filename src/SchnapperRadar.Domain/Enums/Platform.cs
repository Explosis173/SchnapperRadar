namespace SchnapperRadar.Domain.Enums;

public enum Platform
{
    Kleinanzeigen = 1,
    Vinted = 2,
}

[Flags]
public enum PlatformSet
{
    None = 0,
    Kleinanzeigen = 1,
    Vinted = 2,
    All = Kleinanzeigen | Vinted,
}

public static class PlatformSetExtensions
{
    public static bool Contains(this PlatformSet set, Platform platform) =>
        set.HasFlag((PlatformSet)(int)platform);
}
