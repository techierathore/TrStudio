namespace TrStudio.Engine.Infrastructure.Security;

/// <summary>
/// The operating system's protection for the key store (phase 3, ADR-020). It hands
/// <see cref="AesGcmKeyStore"/> the 256-bit key that unlocks the stored provider keys; the key is
/// random, made on first use, and kept by the operating system for the signed-in user (a Keychain
/// item on Mac, a DPAPI current-user blob on Windows). The database only ever holds ciphertext.
/// </summary>
public interface IKeyProtector
{
    /// <summary>Plain words for the screen: where the unlock key is kept.</summary>
    string Description { get; }

    /// <summary>Returns the unlock key, creating and storing it on first use.</summary>
    /// <returns>32 bytes.</returns>
    byte[] GetOrCreateDataKey();
}
