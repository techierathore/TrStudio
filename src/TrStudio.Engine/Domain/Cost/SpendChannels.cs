using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Cost;

/// <summary>
/// Maps a model's <see cref="ProviderKind"/> to the billing <see cref="SpendChannel"/> its spend
/// accrues against (REQ-FN-029): serverless GPU backends bill to RunPod, closed vendor APIs bill
/// to ClosedApi, and everything local is free.
/// </summary>
public static class SpendChannels
{
    /// <summary>
    /// Resolves the billing channel for a provider kind.
    /// </summary>
    /// <param name="aProviderKind">The resolved model's provider kind.</param>
    /// <returns>The billing channel the job's spend accrues against.</returns>
    public static SpendChannel FromProviderKind(ProviderKind aProviderKind) => aProviderKind switch
    {
        ProviderKind.RunPod => SpendChannel.RunPod,
        ProviderKind.ComfyUi => SpendChannel.RunPod,
        ProviderKind.ClosedApi => SpendChannel.ClosedApi,
        ProviderKind.HeyGen => SpendChannel.ClosedApi,
        _ => SpendChannel.Local
    };
}
