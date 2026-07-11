using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace TrStudioApi.Hubs;

/// <summary>
/// SignalR hub pushing live generation-job progress (state, percentage, ETA, failure message
/// and remediation hint) to the Blazor apps, mapped at
/// <see cref="TrStudio.Models.ApiRoutes.ProgressHub"/> (REQ-FN-005).
/// </summary>
/// <remarks>
/// OWNED BY: cluster A (spine). Each authenticated connection joins its per-user group; the
/// engine's <c>ProgressBroadcaster</c> is bridged onto <c>IHubContext&lt;ProgressHub&gt;</c>
/// in <c>SpineEndpoints</c>, sending <c>JobProgress</c> events with
/// <see cref="TrStudio.Models.Contracts.JobProgressUpdate"/> payloads to that group.
/// </remarks>
public class ProgressHub : Hub
{
    /// <summary>The client-side event name carrying <see cref="TrStudio.Models.Contracts.JobProgressUpdate"/> payloads.</summary>
    public const string JobProgressEvent = "JobProgress";

    /// <summary>
    /// Builds the per-user SignalR group name progress updates are routed to.
    /// </summary>
    /// <param name="aUserId">The AppManager user id.</param>
    /// <returns>The group name for that user's connections.</returns>
    public static string UserGroupName(string aUserId) => $"user-{aUserId}";

    /// <summary>
    /// Adds the authenticated connection to its per-user group so job progress published for
    /// that user reaches every open UI surface.
    /// </summary>
    /// <returns>A task completing when the group membership is registered.</returns>
    public override async Task OnConnectedAsync()
    {
        var vUserId = ResolveUserId(Context.User);
        if (!string.IsNullOrEmpty(vUserId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(vUserId)).ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    private static string? ResolveUserId(ClaimsPrincipal? aUser)
        => aUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser?.FindFirst("sub")?.Value;
}
