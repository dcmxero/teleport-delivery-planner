using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// The baseline: ranks parcels at fixed prices and loads them in that order.
/// </summary>
/// <param name="price">What weight and volume cost when ranking parcels.</param>
/// <param name="vanSelection">How a van is chosen for a parcel that is being loaded.</param>
public sealed class GreedyPlanner(ResourcePrice price, IVanSelection vanSelection)
    : IDeliveryPlanner
{
    /// <summary>
    /// Name of the planner and its loading rule.
    /// </summary>
    public string Name => $"greedy/{vanSelection.Name}";

    /// <summary>
    /// Builds a plan for one circuit.
    /// </summary>
    /// <param name="parcels">Everything waiting in the warehouse.</param>
    /// <param name="fleet">The vans available for this circuit.</param>
    public DeliveryPlan Plan(IReadOnlyList<Parcel> parcels, FleetConfiguration fleet)
    {
        ArgumentNullException.ThrowIfNull(parcels);

        Parcel[] candidates = [.. parcels];
        int candidateCount = candidates.Length;

        // Negated, so the ascending sort puts the best parcel first.
        double[] rankingKeys = new double[candidateCount];
        for (int i = 0; i < candidateCount; i++)
        {
            rankingKeys[i] = -price.ScoreOf(candidates[i]);
        }

        Array.Sort(rankingKeys, candidates, 0, candidateCount);

        int[] order = new int[candidateCount];
        for (int i = 0; i < candidateCount; i++)
        {
            order[i] = i;
        }

        VanLoad[] vans = FleetPacking.EmptyFleet(fleet);
        List<int> rejected = [];

        FleetPacking.LoadInOrder(vans, candidates, order, vanSelection, rejected);

        return new DeliveryPlan(fleet, vans, rejected);
    }
}
