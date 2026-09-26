using TeleportDelivery.Core.Domain;
using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Tests;

/// <summary>
/// The idea behind both planners, on the smallest instance that shows it.
/// </summary>
public class GreedyPlannerTests
{
    [Fact]
    public void ManyEfficientParcelsAreTakenOverOneLucrativeHog()
    {
        // One parcel fills the van and pays 1000; nine smaller ones fill it and pay 4500.
        FleetConfiguration single = new(
            VanCount: 1, VanWeightCapacityGrams: 9_000, VanVolumeCapacityCm3: 9_000);

        Parcel[] parcels =
        [
            new(Id: 1, WeightGrams: 9_000, VolumeCm3: 9_000, ProfitHellers: 1_000),
            .. Enumerable.Range(2, 9).Select(id =>
                new Parcel(id, WeightGrams: 1_000, VolumeCm3: 1_000, ProfitHellers: 500)),
        ];

        GreedyPlanner planner = new(ResourcePrice.Balanced(single), new BalancedVanSelection());
        DeliveryPlan plan = planner.Plan(parcels, single);

        Assert.Equal(4_500, plan.TotalProfitHellers);
        Assert.Equal([1], plan.RejectedParcelIds);
    }
}
