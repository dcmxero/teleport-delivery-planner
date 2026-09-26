namespace TeleportDelivery.Core.Planning;

/// <summary>
/// How a plan was arrived at.
/// </summary>
/// <param name="UsedFastPath">
/// Whether the whole pool fitted, so nothing had to be chosen between.
/// </param>
/// <param name="CandidatesEstimated">
/// How many price splits were estimated before the budget ran out.
/// </param>
/// <param name="PlansBuilt">How many full plans were built and compared.</param>
/// <param name="ChosenTheta">
/// Share of cost charged to weight in the winning plan; null on the fast path.
/// </param>
/// <param name="Elapsed">Wall clock time for the whole run.</param>
public readonly record struct PlanningTrace(
    bool UsedFastPath,
    int CandidatesEstimated,
    int PlansBuilt,
    double? ChosenTheta,
    TimeSpan Elapsed);
