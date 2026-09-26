using TeleportDelivery.Core.DataGeneration;
using TeleportDelivery.Core.Domain;
using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Tests;

/// <summary>
/// The ceiling: exact values on cases worked out by hand, and tightness on real pools.
/// </summary>
public class ProfitUpperBoundTests
{
    [Fact]
    public void WhenEverythingFitsTheBoundIsTheWholeProfit()
    {
        FleetConfiguration fleet = new(
            VanCount: 2, VanWeightCapacityGrams: 100_000, VanVolumeCapacityCm3: 100_000);

        Parcel[] parcels =
        [
            new(Id: 1, WeightGrams: 1_000, VolumeCm3: 2_000, ProfitHellers: 500),
            new(Id: 2, WeightGrams: 3_000, VolumeCm3: 1_000, ProfitHellers: 700),
        ];

        Assert.Equal(1_200.0, ProfitUpperBound.Compute(parcels, fleet), precision: 6);
    }

    [Fact]
    public void TheLastParcelIsCountedInPart()
    {
        // Room for one and a half parcels by volume.
        FleetConfiguration fleet = new(
            VanCount: 1, VanWeightCapacityGrams: 1_000_000, VanVolumeCapacityCm3: 1_500);

        Parcel[] parcels =
        [
            new(Id: 1, WeightGrams: 10, VolumeCm3: 1_000, ProfitHellers: 1_000),
            new(Id: 2, WeightGrams: 10, VolumeCm3: 1_000, ProfitHellers: 1_000),
        ];

        Assert.Equal(1_500.0, ProfitUpperBound.Compute(parcels, fleet), precision: 6);
    }

    [Fact]
    public void ParcelsNoVanCouldCarryAreIgnored()
    {
        FleetConfiguration fleet = new(
            VanCount: 1, VanWeightCapacityGrams: 10_000, VanVolumeCapacityCm3: 10_000);

        Parcel[] parcels =
        [
            new(Id: 1, WeightGrams: 1_000, VolumeCm3: 1_000, ProfitHellers: 100),
            new(Id: 2, WeightGrams: 50_000, VolumeCm3: 1_000, ProfitHellers: 1_000_000),
        ];

        Assert.Equal(100.0, ProfitUpperBound.Compute(parcels, fleet), precision: 6);
    }

    [Fact]
    public void TheTighterLimitDecides()
    {
        // Volume allows both parcels, weight only one.
        FleetConfiguration fleet = new(
            VanCount: 1, VanWeightCapacityGrams: 1_000, VanVolumeCapacityCm3: 1_000_000);

        Parcel[] parcels =
        [
            new(Id: 1, WeightGrams: 1_000, VolumeCm3: 10, ProfitHellers: 1_000),
            new(Id: 2, WeightGrams: 1_000, VolumeCm3: 10, ProfitHellers: 1_000),
        ];

        Assert.Equal(1_000.0, ProfitUpperBound.Compute(parcels, fleet), precision: 6);
    }

    [Theory]
    [InlineData("OrdinaryDay")]
    [InlineData("DenseGoods")]
    [InlineData("LowMargin")]
    public void TheBoundIsTightEnoughToBeWorthQuoting(string pool)
    {
        FleetConfiguration fleet = FleetConfiguration.Default;
        Parcel[] parcels = ParcelGenerator.Generate(
            DemandMix.ByName(pool).WithParcelCount(80_000), seed: 5);

        AdaptivePlanner planner = new(new BalancedVanSelection(), PlannerSettings.Unhurried);
        DeliveryPlan plan = planner.Plan(parcels, fleet);

        double reached = plan.TotalProfitHellers / ProfitUpperBound.Compute(parcels, fleet);
        Assert.InRange(reached, 0.995, 1.0);
    }
}
