using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// Decides which parcels travel on one circuit and which van each goes into.
/// </summary>
/// <remarks>
/// Stateless and safe to share between threads; what a run did comes back on
/// <see cref="DeliveryPlan.Trace"/>.
/// </remarks>
public interface IDeliveryPlanner
{
    /// <summary>
    /// Name of the planner in a result table.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds a plan for one circuit.
    /// </summary>
    /// <param name="parcels">Everything waiting in the warehouse.</param>
    /// <param name="fleet">The vans available for this circuit.</param>
    DeliveryPlan Plan(IReadOnlyList<Parcel> parcels, FleetConfiguration fleet);
}
