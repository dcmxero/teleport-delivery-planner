namespace TeleportDelivery.Core.Domain;

/// <summary>
/// The outcome of one planning run: which parcels travel and in which van.
/// </summary>
/// <param name="fleet">The fleet this plan was built for.</param>
/// <param name="vans">The loaded vans, one entry per van in the fleet.</param>
/// <param name="rejectedParcelIds">Parcels left behind for a later circuit.</param>
/// <param name="trace">
/// How the plan was reached, when the planner has anything to say about it.
/// </param>
public sealed class DeliveryPlan(
    FleetConfiguration fleet,
    IReadOnlyList<VanLoad> vans,
    IReadOnlyList<int> rejectedParcelIds,
    Planning.PlanningTrace? trace = null)
{
    /// <summary>
    /// How this plan was reached, or null when the planner had nothing to record.
    /// </summary>
    public Planning.PlanningTrace? Trace { get; } = trace;

    /// <summary>
    /// The fleet this plan was built for.
    /// </summary>
    public FleetConfiguration Fleet { get; } = fleet;

    /// <summary>
    /// The loaded vans, one entry per van in the fleet, empty ones included.
    /// </summary>
    public IReadOnlyList<VanLoad> Vans { get; } = vans;

    /// <summary>
    /// Parcels left for a later circuit.
    /// </summary>
    public IReadOnlyList<int> RejectedParcelIds { get; } = rejectedParcelIds;

    /// <summary>
    /// Total profit carried on this circuit, in hellers.
    /// </summary>
    public long TotalProfitHellers => Vans.Sum(van => van.ProfitHellers);

    /// <summary>
    /// How many parcels leave the warehouse on this circuit.
    /// </summary>
    public int DispatchedParcelCount => Vans.Sum(van => van.ParcelIds.Count);

    /// <summary>
    /// Total weight loaded across the fleet, in grams.
    /// </summary>
    public long UsedWeightGrams => Vans.Sum(van => (long)van.UsedWeightGrams);

    /// <summary>
    /// Total volume loaded across the fleet, in cubic centimetres.
    /// </summary>
    public long UsedVolumeCm3 => Vans.Sum(van => (long)van.UsedVolumeCm3);

    /// <summary>
    /// Share of the fleet's weight capacity used, between 0 and 1.
    /// </summary>
    public double WeightUtilisation => (double)UsedWeightGrams / Fleet.TotalWeightCapacityGrams;

    /// <summary>
    /// Share of the fleet's volume capacity used, between 0 and 1.
    /// </summary>
    public double VolumeUtilisation => (double)UsedVolumeCm3 / Fleet.TotalVolumeCapacityCm3;
}
