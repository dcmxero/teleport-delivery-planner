using TeleportDelivery.Core.DataGeneration;
using TeleportDelivery.Core.Domain;
using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Tests;

/// <summary>
/// Checks the ceiling and the planners against the true optimum on tiny instances.
/// </summary>
/// <remarks>
/// Every quality figure is a share of the ceiling, so the ceiling must never be below an
/// optimum. A regression check on 120 instances, not a proof.
/// </remarks>
public class ExhaustiveComparisonTests
{
    private const int Cases = 120;

    [Fact]
    public void TheOptimumLiesBetweenEveryPlanAndTheCeiling()
    {
        int constrained = 0;
        int nonEmpty = 0;

        for (int caseNumber = 0; caseNumber < Cases; caseNumber++)
        {
            (FleetConfiguration fleet, Parcel[] parcels) = Instance(caseNumber);

            long optimum = BestPossible(parcels, fleet);
            double ceiling = ProfitUpperBound.Compute(parcels, fleet);

            Assert.True(
                optimum <= ceiling + 1e-7,
                $"Case {caseNumber}: optimum {optimum} exceeds the ceiling {ceiling:F4}.");

            IDeliveryPlanner[] planners =
            [
                new GreedyPlanner(ResourcePrice.Balanced(fleet), new BalancedVanSelection()),
                new AdaptivePlanner(new BalancedVanSelection(), PlannerSettings.Unhurried),
            ];

            foreach (IDeliveryPlanner planner in planners)
            {
                long carried = planner.Plan(parcels, fleet).TotalProfitHellers;

                Assert.True(
                    carried <= optimum,
                    $"Case {caseNumber}, {planner.Name}: {carried} beats the optimum {optimum}.");
            }

            if (parcels.Length > 0)
            {
                nonEmpty++;
            }

            if (optimum < parcels.Sum(parcel => parcel.ProfitHellers))
            {
                constrained++;
            }
        }

        // Instances where everything fits would prove nothing.
        Assert.True(
            constrained > nonEmpty / 2,
            $"Only {constrained} of {nonEmpty} instances force a choice.");
    }

    /// <summary>
    /// A small instance, deterministic in the case number, sized so the limits bite.
    /// </summary>
    private static (FleetConfiguration Fleet, Parcel[] Parcels) Instance(int caseNumber)
    {
        const int Capacity = 100;
        Pcg32 random = new((ulong)caseNumber, sequence: 77);

        int vans = 1 + (int)(random.NextDouble() * 3);
        FleetConfiguration fleet = new(
            VanCount: vans, VanWeightCapacityGrams: Capacity, VanVolumeCapacityCm3: Capacity);

        Parcel[] parcels = new Parcel[(int)(random.NextDouble() * 8)];

        for (int i = 0; i < parcels.Length; i++)
        {
            parcels[i] = new Parcel(
                Id: i,
                WeightGrams: 1 + (int)(random.NextDouble() * Capacity),
                VolumeCm3: 1 + (int)(random.NextDouble() * Capacity),
                ProfitHellers: 1 + (long)(random.NextDouble() * 1_000));
        }

        return (fleet, parcels);
    }

    /// <summary>
    /// The best profit, by trying every assignment of every parcel to a van or to none.
    /// </summary>
    private static long BestPossible(Parcel[] parcels, FleetConfiguration fleet) =>
        Search(parcels, fleet, new int[parcels.Length], 0);

    private static long Search(
        Parcel[] parcels, FleetConfiguration fleet, int[] assignment, int position)
    {
        if (position == parcels.Length)
        {
            return Value(parcels, fleet, assignment);
        }

        long best = 0;

        // Zero means left behind; one upwards names a van.
        for (int choice = 0; choice <= fleet.VanCount; choice++)
        {
            assignment[position] = choice;
            best = Math.Max(best, Search(parcels, fleet, assignment, position + 1));
        }

        return best;
    }

    /// <summary>
    /// What an assignment earns, or nothing if it overloads a van.
    /// </summary>
    private static long Value(Parcel[] parcels, FleetConfiguration fleet, int[] assignment)
    {
        Span<int> weight = stackalloc int[fleet.VanCount + 1];
        Span<int> volume = stackalloc int[fleet.VanCount + 1];
        long profit = 0;

        for (int i = 0; i < parcels.Length; i++)
        {
            int van = assignment[i];

            if (van == 0)
            {
                continue;
            }

            weight[van] += parcels[i].WeightGrams;
            volume[van] += parcels[i].VolumeCm3;

            if (weight[van] > fleet.VanWeightCapacityGrams
                || volume[van] > fleet.VanVolumeCapacityCm3)
            {
                return 0;
            }

            profit += parcels[i].ProfitHellers;
        }

        return profit;
    }
}
