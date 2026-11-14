using System.Security.Cryptography;
using TrStudio.Tests.Unit.Venice;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>
/// Phase 3: REQ-NFR-021 — ComfyUI behaves exactly as before. The five ComfyUI test files and the
/// provider keep the content they had when phase 3 started (sha256 taken on 2026-11-14 before any
/// phase-3 change); the 16 ComfyUI tests themselves run in the same suite.
/// </summary>
public class ComfyUnchangedTests
{
    private static readonly (string Path, string Sha256)[] Baseline =
    {
        ("tests/unit/TrStudio.Tests.Unit/Comfy/ComfyDependencyScannerTests.cs", "71b3a07f210e7d46070738988eb84e9896e4eed688365a32c06d811006d81142"),
        ("tests/unit/TrStudio.Tests.Unit/Comfy/ComfyParamMapperTests.cs", "dd99a2568496f06deb9a20e43c8335e8b78dcc2136800b1df5fa066c03603080"),
        ("tests/unit/TrStudio.Tests.Unit/Comfy/ComfyPipelineServiceTests.cs", "f43ea049a585e1f77f527470d7a97cf1b5d97ba32e756f199862391f659cb92d"),
        ("tests/unit/TrStudio.Tests.Unit/Comfy/ComfyUiProviderTests.cs", "a35f88cc83cf2c692a0584c35c7fc8531a61f512461b24545c1b4fbf677776c5"),
        ("tests/unit/TrStudio.Tests.Unit/Comfy/ComfyWorkflowParserTests.cs", "50420d3166f0cc58cf496bbe77b188ad4176d70eeab90a8165734a8abf51976b"),
        ("src/TrStudio.Engine/Providers/ComfyUiProvider.cs", "3a18d6236b34ab103069051c4e7b23860560aea622db7484a72754a971dee590")
    };

    /// <summary>REQ-NFR-021: no ComfyUI test file and not the ComfyUI provider changed.</summary>
    [Fact(DisplayName = "REQ-NFR-021 ComfyUI test files and provider are unchanged")]
    public void ComfyUiFilesAreUnchanged()
    {
        foreach (var (vPath, vExpected) in Baseline)
        {
            var vActual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(RepoPaths.Root, vPath)))).ToLowerInvariant();
            Assert.True(vActual == vExpected, $"{vPath} changed");
        }

        Assert.Equal(5, Directory.GetFiles(Path.Combine(RepoPaths.Root, "tests", "unit", "TrStudio.Tests.Unit", "Comfy"), "*.cs").Length);
    }
}
