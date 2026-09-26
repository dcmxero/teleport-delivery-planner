using TeleportDelivery.Core.DataGeneration;
using TeleportDelivery.Core.Domain;
using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Tests;

/// <summary>
/// What is specific to the planner that chooses its own prices.
/// </summary>
public class AdaptivePlannerTests
{
    private static readonly FleetConfiguration Fleet = FleetConfiguration.Default;

    /// <summary>
    /// Every day, plus the one where adaptive used to earn less before the fixed-price plan
    /// became a finalist.
    /// </summary>
    public static TheoryData<string, ulong> AllPools()
    {
        TheoryData<string, ulong> data = [];

        foreach (DemandMix mix in DemandMix.All)
        {
            data.Add(mix.Name, 5);
        }

        data.Add("LowMargin", 4);
        return data;
    }

    private static AdaptivePlanner Planner() =>
        new(new BalancedVanSelection(), PlannerSettings.Unhurried);

    private static GreedyPlanner Baseline() =>
        new(ResourcePrice.Balanced(Fleet), new BalancedVanSelection());

    private static Parcel[] Parcels(string mixName, int count = 80_000, ulong seed = 5) =>
        ParcelGenerator.Generate(DemandMix.ByName(mixName).WithParcelCount(count), seed);

    [Theory]
    [MemberData(nameof(AllPools))]
    public void ChoosingPricesIsNeverWorseThanAssumingThem(string pool, ulong seed)
    {
        Parcel[] parcels = Parcels(pool, seed: seed);

        long baseline = Baseline().Plan(parcels, Fleet).TotalProfitHellers;
        long adaptive = Planner().Plan(parcels, Fleet).TotalProfitHellers;

        Assert.True(
            adaptive >= baseline, $"{pool}/{seed}: adaptive {adaptive}, baseline {baseline}.");
    }

    [Theory]
    [InlineData("MixedExtremes", 1.03)]
    [InlineData("DenseGoods", 1.02)]
    public void ChoosingPricesPaysWhereCapacityIsScarce(string pool, double gain)
    {
        // Full-size pools: at 80 000 parcels the dense pool is barely over capacity.
        Parcel[] parcels = ParcelGenerator.Generate(DemandMix.ByName(pool), seed: 5);

        long baseline = Baseline().Plan(parcels, Fleet).TotalProfitHellers;
        long adaptive = Planner().Plan(parcels, Fleet).TotalProfitHellers;

        Assert.True(
            adaptive > baseline * gain, $"{pool}: baseline {baseline}, adaptive {adaptive}.");
    }

    [Fact]
    public void PricingFollowsWhicheverResourceIsScarce()
    {
        AdaptivePlanner planner = Planner();

        Assert.InRange(
            planner.Plan(Parcels("BulkyGoods"), Fleet).Trace!.Value.ChosenTheta!.Value, 0.0, 0.25);
        Assert.InRange(
            planner.Plan(Parcels("DenseGoods"), Fleet).Trace!.Value.ChosenTheta!.Value, 0.75, 1.0);
    }

    [Fact]
    public void AQuietDaySkipsTheSearch()
    {
        DeliveryPlan plan = Planner().Plan(Parcels("QuietDay"), Fleet);

        Assert.True(plan.Trace!.Value.UsedFastPath);
        Assert.Equal(0, plan.Trace.Value.CandidatesEstimated);
        Assert.Empty(plan.RejectedParcelIds);
    }

    [Fact]
    public void FittingInTotalDoesNotMeanFittingInIndividualVans()
    {
        // 18 kg fits two 10 kg vans in total, but three 6 kg parcels do not.
        FleetConfiguration fleet = new(
            VanCount: 2, VanWeightCapacityGrams: 10_000, VanVolumeCapacityCm3: 10_000);

        Parcel[] parcels =
        [
            new(Id: 1, WeightGrams: 6_000, VolumeCm3: 6_000, ProfitHellers: 100),
            new(Id: 2, WeightGrams: 6_000, VolumeCm3: 6_000, ProfitHellers: 100),
            new(Id: 3, WeightGrams: 6_000, VolumeCm3: 6_000, ProfitHellers: 900),
        ];

        DeliveryPlan plan = Planner().Plan(parcels, fleet);

        // The fast path fails and the search keeps the two most profitable.
        Assert.False(plan.Trace!.Value.UsedFastPath);
        Assert.Single(plan.RejectedParcelIds);
        Assert.Equal(1_000, plan.TotalProfitHellers);
    }

    [Fact]
    public void RunningOutOfTimeStillReturnsAPlan()
    {
        AdaptivePlanner starved = new(
            new BalancedVanSelection(),
            AdaptivePlannerOptions.Default with { TimeBudget = TimeSpan.FromTicks(1) });

        Parcel[] parcels = Parcels("MixedExtremes");
        DeliveryPlan plan = starved.Plan(parcels, Fleet);

        // Only the fixed-price plan, and the trace counts only the estimates actually made.
        Assert.Equal(1, plan.Trace!.Value.PlansBuilt);
        Assert.True(
            plan.Trace.Value.CandidatesEstimated < AdaptivePlannerOptions.Default.PriceCandidates);
        Assert.Equal(Baseline().Plan(parcels, Fleet).TotalProfitHellers, plan.TotalProfitHellers);
    }

    [Fact]
    public void RankingPutsTheBestFirstAndCanBeReused()
    {
        Parcel[] parcels =
        [
            new(Id: 0, WeightGrams: 100, VolumeCm3: 100_000, ProfitHellers: 1_000),
            new(Id: 1, WeightGrams: 100_000, VolumeCm3: 100, ProfitHellers: 1_000),
            new(Id: 2, WeightGrams: 1_000, VolumeCm3: 1_000, ProfitHellers: 1),
        ];

        ProfitDensityRanking ranking = new(parcels.Length);

        // Charging only volume makes the bulky parcel the worse buy; only weight, the heavy one.
        ranking.Rank(parcels, ResourcePrice.Split(Fleet, theta: 0.0));
        Assert.Equal([1, 0, 2], ranking.Order.ToArray());

        ranking.Rank(parcels, ResourcePrice.Split(Fleet, theta: 1.0));
        Assert.Equal([0, 1, 2], ranking.Order.ToArray());
    }
}
