using System.Security.Cryptography;
using TrStudio.Engine.Infrastructure.Security;

namespace TrStudio.TestHost;

/// <summary>
/// TEST ONLY: the test host's stand-in for the operating system's key protection. The unlock key is
/// random per run and lives in memory only, so the headless checks can save keys on Registry and
/// providers. The desktop app uses the Keychain or Windows data protection instead (ADR-020).
/// </summary>
public sealed class TestKeyProtector : IKeyProtector
{
    private readonly byte[] objKey = RandomNumberGenerator.GetBytes(32);

    /// <inheritdoc />
    public string Description => "a test-only key held in memory by the test host";

    /// <inheritdoc />
    public byte[] GetOrCreateDataKey() => objKey;
}
