using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrStudio.Engine.Infrastructure.Security;

namespace TrStudio.InProcess.Hosting;

/// <summary>
/// "Remember me" on the sign-in screen (owner decision 6B, ADR-021, BRD-153 to BRD-157). When the box
/// is ticked, the AppManager refresh token is kept in one file in the user's own data folder, encrypted
/// with the same unlock key the operating system keeps for the user's provider keys (ADR-020), so it is
/// never on disk in plain text. On the next open the app signs in with it without asking. It is kept
/// for <see cref="RememberDays"/> days from the ticked sign-in; after that, or when AppManager refuses
/// the token, the file is removed and the user signs in again. Signing out always removes it.
/// </summary>
public sealed class RememberedSignInStore
{
    /// <summary>How long a ticked sign-in is kept on this computer.</summary>
    public const int RememberDays = 30;

    /// <summary>The file name, in the user's data folder.</summary>
    public const string FileName = "remembered-sign-in.bin";

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly string objPath;
    private readonly IKeyProtector objProtector;
    private readonly TimeProvider objClock;
    private readonly object objGate = new();

    /// <summary>Creates the store over the user's data folder.</summary>
    /// <param name="aDataFolder">The user's TrStudio data folder.</param>
    /// <param name="aProtector">The operating system's protection for the unlock key.</param>
    /// <param name="aClock">The clock (tests move it forward).</param>
    public RememberedSignInStore(string aDataFolder, IKeyProtector aProtector, TimeProvider? aClock = null)
    {
        objPath = Path.Combine(aDataFolder, FileName);
        objProtector = aProtector;
        objClock = aClock ?? TimeProvider.System;
    }

    /// <summary>Full path of the kept file (tests read it to prove it is not plain text).</summary>
    public string FilePath => objPath;

    /// <summary>Whether a sign-in is kept on this computer.</summary>
    public bool HasKept => File.Exists(objPath);

    /// <summary>Keeps a ticked sign-in for <see cref="RememberDays"/> days from now.</summary>
    /// <param name="aRefreshToken">The AppManager refresh token.</param>
    /// <param name="aEmail">The sign-in email (shown in the shell chip).</param>
    public void Remember(string aRefreshToken, string? aEmail)
        => Write(new RememberedSignIn(aRefreshToken, aEmail, objClock.GetUtcNow().AddDays(RememberDays)));

    /// <summary>Replaces the kept refresh token after AppManager renewed it, keeping the original end date.</summary>
    /// <param name="aKept">What was kept.</param>
    /// <param name="aNewRefreshToken">The renewed refresh token.</param>
    public void Renew(RememberedSignIn aKept, string aNewRefreshToken)
    {
        ArgumentNullException.ThrowIfNull(aKept);
        if (!string.IsNullOrWhiteSpace(aNewRefreshToken))
        {
            Write(aKept with { RefreshToken = aNewRefreshToken });
        }
    }

    /// <summary>Reads what is kept.</summary>
    /// <returns>The kept sign-in, or null when nothing is kept or it cannot be unlocked on this computer.</returns>
    public RememberedSignIn? Read()
    {
        lock (objGate)
        {
            if (!File.Exists(objPath))
            {
                return null;
            }

            try
            {
                var vPayload = File.ReadAllBytes(objPath);
                var vPlain = Decrypt(vPayload);
                return JsonSerializer.Deserialize<RememberedSignIn>(vPlain);
            }
            catch (Exception vException) when (vException is CryptographicException or JsonException or IOException or ArgumentException)
            {
                // Not unlockable with this user's key (another user, a removed Keychain item): forget it.
                TryDelete();
                return null;
            }
        }
    }

    /// <summary>Whether a kept sign-in is past its end date.</summary>
    /// <param name="aKept">What was kept.</param>
    /// <returns>True when it has ended.</returns>
    public bool HasEnded(RememberedSignIn aKept)
    {
        ArgumentNullException.ThrowIfNull(aKept);
        return objClock.GetUtcNow() >= aKept.KeptUntil;
    }

    /// <summary>Removes what is kept (sign out, an unticked sign-in, an ended or refused token).</summary>
    public void Forget()
    {
        lock (objGate)
        {
            TryDelete();
        }
    }

    private void Write(RememberedSignIn aKept)
    {
        lock (objGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(objPath)!);
            var vPlain = JsonSerializer.SerializeToUtf8Bytes(aKept);
            var vTemp = objPath + ".tmp";
            File.WriteAllBytes(vTemp, Encrypt(vPlain));
            File.Move(vTemp, objPath, overwrite: true);
        }
    }

    private byte[] Encrypt(byte[] aPlain)
    {
        var vNonce = RandomNumberGenerator.GetBytes(NonceSize);
        var vCipher = new byte[aPlain.Length];
        var vTag = new byte[TagSize];
        using (var vAes = new AesGcm(objProtector.GetOrCreateDataKey(), TagSize))
        {
            vAes.Encrypt(vNonce, aPlain, vCipher, vTag, Encoding.UTF8.GetBytes(FileName));
        }

        var vPayload = new byte[NonceSize + TagSize + vCipher.Length];
        Buffer.BlockCopy(vNonce, 0, vPayload, 0, NonceSize);
        Buffer.BlockCopy(vTag, 0, vPayload, NonceSize, TagSize);
        Buffer.BlockCopy(vCipher, 0, vPayload, NonceSize + TagSize, vCipher.Length);
        return vPayload;
    }

    private byte[] Decrypt(byte[] aPayload)
    {
        if (aPayload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The kept sign-in file is too short.");
        }

        var vNonce = aPayload.AsSpan(0, NonceSize);
        var vTag = aPayload.AsSpan(NonceSize, TagSize);
        var vCipher = aPayload.AsSpan(NonceSize + TagSize);
        var vPlain = new byte[vCipher.Length];
        using var vAes = new AesGcm(objProtector.GetOrCreateDataKey(), TagSize);
        vAes.Decrypt(vNonce, vCipher, vTag, vPlain, Encoding.UTF8.GetBytes(FileName));
        return vPlain;
    }

    private void TryDelete()
    {
        try
        {
            File.Delete(objPath);
        }
        catch (IOException)
        {
            // Removed on the next start.
        }
        catch (UnauthorizedAccessException)
        {
            // Removed on the next start.
        }
    }
}

/// <summary>A sign-in kept on this computer.</summary>
/// <param name="RefreshToken">The AppManager refresh token (never logged, never shown).</param>
/// <param name="Email">The sign-in email.</param>
/// <param name="KeptUntil">When the kept sign-in ends (30 days after the ticked sign-in).</param>
public sealed record RememberedSignIn(string RefreshToken, string? Email, DateTimeOffset KeptUntil);
