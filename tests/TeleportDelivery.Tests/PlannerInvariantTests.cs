using TeleportDelivery.Core.DataGeneration;
using TeleportDelivery.Core.Domain;
using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Tests;

/// <summary>
/// What must hold of any plan, whichever planner produced it.
/// </summary>
/// <remarks>
/// Run on the shipping settings: an invariant has to hold however much search fits the budget.
/// </remarks>
public class PlannerInvariantTests
{
    private static readonly FleetConfiguration Fleet = FleetConfiguration.Default;

    /// <summary>
    /// Enough to overflow the fleet on ordinary days, few enough to keep the suite quick.
    /// </summary>
    private const int ParcelCount = 80_000;

    public static TheoryData<string, string> EveryPlannerAndPool()
    {
        TheoryData<string, string> data = [];

        foreach (DemandMix mix in DemandMix.All)
        {
            data.Add("baseline", mix.Name);
            data.Add("adaptive", mix.Name);
        }

        return data;
    }

    public static TheoryData<string> EveryPlanner() => ["baseline", "adaptive"];

    private static IDeliveryPlanner PlannerFor(string name, FleetConfiguration fleet) => name switch
    {
        "baseline" => new GreedyPlanner(ResourcePrice.Balanced(fleet), new BalancedVanSelection()),
        _ => new AdaptivePlanner(new BalancedVanSelection()),
    };

    [Theory]
    [MemberData(nameof(EveryPlannerAndPool))]
    public void EveryPlanIsValid(string planner, string pool)
    {
        Parcel[] parcels = ParcelGenerator.Generate(
            DemandMix.ByName(pool).WithParcelCount(ParcelCount), seed: 5);
        DeliveryPlan plan = PlannerFor(planner, Fleet).Plan(parcels, Fleet);
        Dictionary<int, Parcel> byId = parcels.ToDictionary(parcel => parcel.Id);

        // Carried and rejected together are exactly the input, with nothing in both.
        int[] carried = [.. plan.Vans.SelectMany(van => van.ParcelIds)];
        Assert.Equal(carried.Length, carried.Distinct().Count());
        Assert.Empty(carried.Intersect(plan.RejectedParcelIds));
        Assert.Equal(
            parcels.Select(parcel => parcel.Id).Order(),
            carried.Concat(plan.RejectedParcelIds).Order());

        // Each van's load, recomputed from its parcels, matches what it reports and fits.
        foreach (VanLoad van in plan.Vans)
        {
            Parcel[] load = [.. van.ParcelIds.Select(id => byId[id])];

            Assert.Equal(load.Sum(parcel => parcel.WeightGrams), van.UsedWeightGrams);
            Assert.Equal(load.Sum(parcel => parcel.VolumeCm3), van.UsedVolumeCm3);
            Assert.Equal(load.Sum(parcel => parcel.ProfitHellers), van.ProfitHellers);
            Assert.True(
                van.UsedWeightGrams <= Fleet.VanWeightCapacityGrams,
                $"Van {van.Index} is overweight.");
            Assert.True(
                van.UsedVolumeCm3 <= Fleet.VanVolumeCapacityCm3,
                $"Van {van.Index} is overfull.");
        }

        Assert.Equal(carried.Sum(id => byId[id].ProfitHellers), plan.TotalProfitHellers);
        Assert.True(plan.TotalProfitHellers <= ProfitUpperBound.Compute(parcels, Fleet));
    }

    [Theory]
    [MemberData(nameof(EveryPlanner))]
    public void AnEmptyWarehouseGivesAnEmptyPlan(string planner)
    {
        DeliveryPlan plan = PlannerFor(planner, Fleet).Plan([], Fleet);

        Assert.Equal(Fleet.VanCount, plan.Vans.Count);
        Assert.Equal(0, plan.TotalProfitHellers);
        Assert.Empty(plan.RejectedParcelIds);
        Assert.Equal(0.0, ProfitUpperBound.Compute([], Fleet));
    }

    [Theory]
    [MemberData(nameof(EveryPlanner))]
    public void ParcelsLargerThanAVanAreLeftBehind(string planner)
    {
        Parcel[] parcels =
        [
            new(Id: 1, WeightGrams: 1_000, VolumeCm3: 1_000, ProfitHellers: 100),
            new(Id: 2, WeightGrams: Fleet.VanWeightCapacityGrams + 1, VolumeCm3: 1_000,
                ProfitHellers: 999_999),
            new(Id: 3, WeightGrams: 1_000, VolumeCm3: Fleet.VanVolumeCapacityCm3 + 1,
                ProfitHellers: 999_999),
        ];

        DeliveryPlan plan = PlannerFor(planner, Fleet).Plan(parcels, Fleet);

        Assert.Equal([2, 3], plan.RejectedParcelIds.Order());
        Assert.Equal(100, plan.TotalProfitHellers);
    }

    [Theory]
    [MemberData(nameof(EveryPlanner))]
    public void LargeAndSparseParcelIdsAreHandled(string planner)
    {
        FleetConfiguration fleet = new(
            VanCount: 1, VanWeightCapacityGrams: 10_000, VanVolumeCapacityCm3: 10_000);

        Parcel[] parcels =
        [
            new(Id: 7, WeightGrams: 9_000, VolumeCm3: 9_000, ProfitHellers: 100),
            new(Id: int.MaxValue - 1, WeightGrams: 9_000, VolumeCm3: 9_000, ProfitHellers: 1_000),
        ];

        DeliveryPlan plan = PlannerFor(planner, fleet).Plan(parcels, fleet);

        Assert.Equal(1_000, plan.TotalProfitHellers);
        Assert.Equal([7], plan.RejectedParcelIds);
    }
}
