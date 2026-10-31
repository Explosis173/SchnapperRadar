namespace SchnapperRadar.Api.Security;

public sealed class AuthOptions
{
    public const string Section = "Auth";
    public const int MinPasswordLength = 12;

    /// <summary>Pflicht außerhalb von Development. Per Env: Auth__Password</summary>
    public string? Password { get; set; }
    /// <summary>Always (Cloud/HTTPS) oder SameAsRequest (lokal über http im LAN).</summary>
    public string CookieSecure { get; set; } = "Always";
    public int SessionDays { get; set; } = 14;
}
