using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// Default <see cref="IProvenanceStamper"/> (SAFE-04 / BRD-63). Builds a C2PA-style provenance /
/// AI-disclosure manifest embedded on EVERY output: it declares AI generation, the model/job/seed,
/// a SHA-256 hash of the content and an HMAC signature over the manifest so it is non-trivial to
/// strip or tamper with silently. Provider watermarks are recorded, never removed.
/// </summary>
public class ProvenanceStamper : IProvenanceStamper
{
    private const string ClaimGenerator = "TrStudio";
    private const string SpecVersion = "c2pa-style/1.0";

    private readonly SafetyOptions objOptions;

    /// <summary>
    /// Initialises the stamper.
    /// </summary>
    /// <param name="aOptions">Safety options carrying the manifest signing key.</param>
    public ProvenanceStamper(SafetyOptions aOptions)
    {
        objOptions = aOptions;
    }

    /// <inheritdoc />
    public string BuildManifest(GeneratedAsset aAsset, byte[] aContent, SafetyEvaluationContext aContext)
    {
        var vContentHash = Convert.ToHexString(SHA256.HashData(aContent)).ToLowerInvariant();

        // The claim is the signed payload: hashing the content + core metadata means a stripped or
        // altered manifest no longer verifies against the asset (SAFE-04: non-trivial to strip).
        var vClaim = new
        {
            specVersion = SpecVersion,
            claimGenerator = ClaimGenerator,
            aiGenerated = true,
            aiDisclosure = "This media was generated or materially edited by AI.",
            assetId = aAsset.AssetId,
            jobId = aAsset.JobId,
            modelId = aContext.ModelId,
            task = aContext.Task.ToString(),
            tier = aContext.Tier.ToString(),
            mediaKind = aAsset.Kind,
            seed = aAsset.SeedUsed,
            contentHashAlg = "sha256",
            contentHash = vContentHash,
            createdUtc = DateTime.UtcNow
        };

        var vClaimJson = JsonSerializer.Serialize(vClaim);
        var vSignature = Sign(vClaimJson);

        var vManifest = new
        {
            claim = vClaim,
            signature = new
            {
                alg = "hmac-sha256",
                value = vSignature
            }
        };

        return JsonSerializer.Serialize(vManifest);
    }

    private string Sign(string aClaimJson)
    {
        using var vHmac = new HMACSHA256(Encoding.UTF8.GetBytes(objOptions.ProvenanceSigningKey));
        var vHash = vHmac.ComputeHash(Encoding.UTF8.GetBytes(aClaimJson));
        return Convert.ToHexString(vHash).ToLowerInvariant();
    }
}
