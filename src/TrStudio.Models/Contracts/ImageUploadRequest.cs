namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for uploading an image input (a mask for inpaint/outpaint, or a reference /
/// conditioning image for ControlNet) so it can be referenced by a later generation job
/// (<c>POST /api/v1/images/uploads</c>, REQ-FN-020/021). The binary is supplied base64-encoded
/// so the endpoint is JSON- and curl-friendly; it is stored owner-scoped and surfaced as a
/// <see cref="AssetResponse"/> whose <see cref="AssetResponse.AssetId"/> is passed back in a
/// job's params.
/// </summary>
public class ImageUploadRequest
{
    /// <summary>The original file name (used only to derive the stored file extension).</summary>
    public string FileName { get; set; } = "upload.png";

    /// <summary>
    /// The input role: <c>mask</c> (inpaint/outpaint), <c>reference</c> or <c>control</c>
    /// (ControlNet conditioning). Recorded as the stored asset's <see cref="AssetResponse.Kind"/>.
    /// </summary>
    public string Kind { get; set; } = "mask";

    /// <summary>The image bytes, base64-encoded.</summary>
    public string Base64Content { get; set; } = string.Empty;

    /// <summary>The project to attach the upload to, if any.</summary>
    public Guid? ProjectId { get; set; }
}
