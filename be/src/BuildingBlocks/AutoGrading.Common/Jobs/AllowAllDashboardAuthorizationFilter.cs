using Hangfire.Dashboard;

namespace AutoGrading.Common.Jobs;

/// <summary>
/// Allows any request to view the Hangfire dashboard. Hangfire's default
/// LocalRequestsOnlyAuthorizationFilter rejects requests proxied through Docker
/// port mapping (they never appear as loopback), which would make the dashboard
/// unreachable from outside the container. Scaffold-phase only — replace with a
/// real policy (e.g. admin-role JWT check) before exposing this publicly.
/// </summary>
public sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
