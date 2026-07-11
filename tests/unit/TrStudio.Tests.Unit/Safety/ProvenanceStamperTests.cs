using System.Text.Json;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Safety;

/// <summary>
/// Unit tests for <see cref="ProvenanceStamper"/> (FN-059 / SAFE-04): every output carries a
/// C2PA-style manifest declaring AI generation, a content hash and a manifest signature that binds
/// the manifest to the content (non-trivial to strip silently).
/// </summary>
public class ProvenanceStamperTests
{
    private readonly SafetyOptions objOptions = new() { ProvenanceSigningKey = "unit-test-key" };

    private static GeneratedAsset Asset() => new()
    {
        AssetId = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        UserId = "user-1",
        Kind = "image",
        ModelId = "stub-echo",
        SeedUsed = 42
    };

    private static SafetyEvaluationContext Context() => new()
    {
        UserId = "user-1",
        ModelId = "stub-echo",
        Task = GenerationTaskType.TextToImage,
        Tier = GenerationTier.Fast
    };

    /// <summary>The manifest declares AI generation, the disclosure, a sha256 content hash and a signature.</summary>
    [Fact]
    public void ManifestDeclaresAiGenerationHashAndSignature()
    {
        var vManifest = new ProvenanceStamper(objOptions).BuildManifest(Asset(), new byte[] { 1, 2, 3, 4 }, Context());

        using var vDocument = JsonDocument.Parse(vManifest);
        var vClaim = vDocument.RootElement.GetProperty("claim");
        Assert.True(vClaim.GetProperty("aiGenerated").GetBoolean());
        Assert.Equal("sha256", vClaim.GetProperty("contentHashAlg").GetString());
        Assert.False(string.IsNullOrWhiteSpace(vClaim.GetProperty("contentHash").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            vDocument.RootElement.GetProperty("signature").GetProperty("value").GetString()));
    }

    /// <summary>Different content yields a different content hash (the manifest binds to the bytes).</summary>
    [Fact]
    public void DifferentContentYieldsDifferentHash()
    {
        var vStamper = new ProvenanceStamper(objOptions);
        var vAsset = Asset();

        var vHashA = HashOf(vStamper.BuildManifest(vAsset, new byte[] { 1, 2, 3 }, Context()));
        var vHashB = HashOf(vStamper.BuildManifest(vAsset, new byte[] { 9, 9, 9 }, Context()));

        Assert.NotEqual(vHashA, vHashB);
    }

    private static string? HashOf(string aManifest)
    {
        using var vDocument = JsonDocument.Parse(aManifest);
        return vDocument.RootElement.GetProperty("claim").GetProperty("contentHash").GetString();
    }
}
