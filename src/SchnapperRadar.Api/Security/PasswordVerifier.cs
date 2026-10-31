using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SchnapperRadar.Api.Security;

public sealed class PasswordVerifier(IOptions<AuthOptions> options)
{
    private readonly byte[] _expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.Password ?? string.Empty));

    public bool IsConfigured => !string.IsNullOrEmpty(options.Value.Password);

    /// <summary>Konstante Laufzeit – kein Timing-Leak.</summary>
    public bool Verify(string? candidate) =>
        IsConfigured &&
        CryptographicOperations.FixedTimeEquals(_expectedHash, SHA256.HashData(Encoding.UTF8.GetBytes(candidate ?? string.Empty)));
}
