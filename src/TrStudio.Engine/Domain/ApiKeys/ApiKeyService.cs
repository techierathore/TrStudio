using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.ApiKeys;

/// <summary>
/// Default <see cref="IApiKeyService"/> (REQ-FN-069/BRD-66). Generates high-entropy keys prefixed
/// <c>trsk_</c>, stores ONLY their SHA-256 hash (NFR-05), and resolves a presented key back to its
/// owning user + scope for the <c>X-Api-Key</c> auth scheme. The default scope grants the same
/// job-submission surface an interactive session has, without weakening the JWT path.
/// </summary>
public class ApiKeyService : IApiKeyService
{
    /// <summary>The non-secret prefix every issued key carries.</summary>
    public const string KeyPrefix = "trsk_";

    /// <summary>The default scope granted when none is specified.</summary>
    public const string DefaultScope = "jobs";

    private const int SecretBytes = 32;

    private readonly IApiKeyRepository objRepository;
    private readonly ILogger<ApiKeyService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aRepository">The API-key persistence.</param>
    /// <param name="aLogger">Structured logger (never logs plaintext keys).</param>
    public ApiKeyService(IApiKeyRepository aRepository, ILogger<ApiKeyService> aLogger)
    {
        objRepository = aRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<ApiKeyIssuedResponse> IssueAsync(string aUserId, string aName, string? aScope, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aName))
        {
            throw new ArgumentException("A key name is required.", nameof(aName));
        }

        var vSecret = Base64Url(RandomNumberGenerator.GetBytes(SecretBytes));
        var vPlaintext = KeyPrefix + vSecret;
        var vPrefix = KeyPrefix + vSecret[..Math.Min(8, vSecret.Length)];
        var vScope = string.IsNullOrWhiteSpace(aScope) ? DefaultScope : aScope.Trim();

        var vEntity = new ApiKey
        {
            ApiKeyId = Guid.NewGuid(),
            UserId = aUserId,
            Name = aName.Trim(),
            Prefix = vPrefix,
            KeyHash = Hash(vPlaintext),
            Scope = vScope,
            CreatedUtc = DateTime.UtcNow,
            LastUsedUtc = null,
            RevokedUtc = null
        };

        await objRepository.InsertAsync(vEntity, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("Issued API key {ApiKeyId} ({Prefix}) for user {UserId}", vEntity.ApiKeyId, vEntity.Prefix, aUserId);

        return new ApiKeyIssuedResponse
        {
            Key = ToResponse(vEntity),
            PlaintextKey = vPlaintext
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiKeyResponse>> ListAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        var vKeys = await objRepository.ListByUserAsync(aUserId, aCancellationToken).ConfigureAwait(false);
        return vKeys.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public Task<bool> RevokeAsync(Guid aApiKeyId, string aUserId, CancellationToken aCancellationToken = default)
        => objRepository.RevokeAsync(aApiKeyId, aUserId, aCancellationToken);

    /// <inheritdoc />
    public async Task<ApiKeyAuthResult?> AuthenticateAsync(string? aPresentedKey, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aPresentedKey) || !aPresentedKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var vHash = Hash(aPresentedKey);
        var vKey = await objRepository.GetActiveByHashAsync(vHash, aCancellationToken).ConfigureAwait(false);
        if (vKey is null)
        {
            return null;
        }

        // Best-effort last-used stamp; a failure here must not fail the authenticated request.
        try
        {
            await objRepository.TouchUsedAsync(vKey.ApiKeyId, DateTime.UtcNow, aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogDebug(vException, "Failed to stamp last-used on API key {ApiKeyId}", vKey.ApiKeyId);
        }

        return new ApiKeyAuthResult(vKey.UserId, vKey.Scope, vKey.ApiKeyId);
    }

    private static ApiKeyResponse ToResponse(ApiKey aKey) => new()
    {
        ApiKeyId = aKey.ApiKeyId,
        Name = aKey.Name,
        Prefix = aKey.Prefix,
        Scope = aKey.Scope,
        CreatedUtc = aKey.CreatedUtc,
        LastUsedUtc = aKey.LastUsedUtc,
        RevokedUtc = aKey.RevokedUtc
    };

    /// <summary>Computes the lowercase hex SHA-256 hash of a key — the only representation stored.</summary>
    public static string Hash(string aKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(aKey))).ToLowerInvariant();

    private static string Base64Url(byte[] aBytes)
        => Convert.ToBase64String(aBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
