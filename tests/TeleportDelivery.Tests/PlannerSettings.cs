using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Tests;

/// <summary>
/// Planner settings shared by the tests.
/// </summary>
internal static class PlannerSettings
{
    /// <summary>
    /// A budget that never binds, for tests about plan quality.
    /// </summary>
    /// <remarks>
    /// Under the shipping 250 ms such a test would measure the machine, not the code.
    /// </remarks>
    public static AdaptivePlannerOptions Unhurried { get; } = AdaptivePlannerOptions.Default with
    {
        TimeBudget = TimeSpan.FromMinutes(5),
    };
}
