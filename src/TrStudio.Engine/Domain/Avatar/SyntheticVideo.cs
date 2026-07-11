namespace TrStudio.Engine.Domain.Avatar;

/// <summary>
/// Builds a deterministic placeholder MP4-container binary used by the avatar providers when a real
/// lip-sync backend is not wired (dev synthetic path / test fixture, FN-037). The bytes are a minimal
/// ISO base-media <c>ftyp</c> box followed by a small padded payload — enough to prove the pipeline
/// and asset plumbing without a real renderer.
/// </summary>
internal static class SyntheticVideo
{
    /// <summary>
    /// Builds a small deterministic placeholder MP4 byte array.
    /// </summary>
    /// <returns>A byte array beginning with a valid <c>ftyp</c> box.</returns>
    public static byte[] BuildPlaceholderMp4()
    {
        // ftyp box: size(24) + 'ftyp' + major_brand 'isom' + minor_version(512) + compatible 'isom''mp42'.
        var vFtyp = new byte[]
        {
            0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70,
            0x69, 0x73, 0x6F, 0x6D, 0x00, 0x00, 0x02, 0x00,
            0x69, 0x73, 0x6F, 0x6D, 0x6D, 0x70, 0x34, 0x32
        };

        // A small deterministic 'free' box payload so the file has stable, non-trivial content.
        var vFree = new byte[]
        {
            0x00, 0x00, 0x00, 0x10, 0x66, 0x72, 0x65, 0x65,
            0x54, 0x72, 0x53, 0x74, 0x75, 0x64, 0x69, 0x6F
        };

        var vBytes = new byte[vFtyp.Length + vFree.Length];
        Buffer.BlockCopy(vFtyp, 0, vBytes, 0, vFtyp.Length);
        Buffer.BlockCopy(vFree, 0, vBytes, vFtyp.Length, vFree.Length);
        return vBytes;
    }
}
