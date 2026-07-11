using System.Security.Claims;
using Hangfire.Dashboard;

namespace TrStudioApi.Infrastructure;

/// <summary>
/// Hangfire dashboard authorization filter (REQ-FN-003/004): the dashboard is admin-only —
/// the request's authenticated principal must carry a <c>role</c> claim of Owner or Operator.
/// Defence-in-depth alongside the <c>AdminOnly</c> endpoint policy applied to the dashboard route.
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Authorizes a dashboard request.
    /// </summary>
    /// <param name="aContext">The dashboard request context.</param>
    /// <returns><c>true</c> only for authenticated Owner/Operator principals.</returns>
    public bool Authorize(DashboardContext aContext)
    {
        var vUser = aContext.GetHttpContext().User;
        if (vUser.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return vUser.Claims.Any(aClaim =>
            (aClaim.Type == "role" || aClaim.Type == ClaimTypes.Role)
            && aClaim.Value is "Owner" or "Operator");
    }
}
