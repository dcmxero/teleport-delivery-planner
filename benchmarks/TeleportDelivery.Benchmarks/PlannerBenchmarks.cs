using BenchmarkDotNet.Attributes;
using TeleportDelivery.Core.DataGeneration;
using TeleportDelivery.Core.Domain;
using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Benchmarks;

/// <summary>
/// How long a plan takes on different demand mixes.
/// </summary>
/// <remarks>
/// Parcels are generated in setup, outside the measurement.
/// </remarks>
[MemoryDiagnoser]
public class PlannerBenchmarks
{
    private static readonly FleetConfiguration Fleet = FleetConfiguration.Default;

    private Parcel[] parcels = [];
    private GreedyPlanner baseline = null!;
    private AdaptivePlanner adaptive = null!;

    /// <summary>
    /// Everything fits, ordinary, volume-bound, weight-bound, and both limits at once.
    /// </summary>
    [Params("QuietDay", "OrdinaryDay", "BulkyGoods", "DenseGoods", "MixedExtremes")]
    public string Mix { get; set; } = "OrdinaryDay";

    [GlobalSetup]
    public void Setup()
    {
        parcels = ParcelGenerator.Generate(DemandMix.ByName(Mix), seed: 1);
        baseline = new GreedyPlanner(ResourcePrice.Balanced(Fleet), new BalancedVanSelection());
        adaptive = new AdaptivePlanner(new BalancedVanSelection());
    }

    /// <summary>
    /// Fixed prices and a single plan.
    /// </summary>
    [Benchmark(Baseline = true)]
    public long Baseline() => baseline.Plan(parcels, Fleet).TotalProfitHellers;

    /// <summary>
    /// Prices chosen from the demand, with several plans built and compared.
    /// </summary>
    [Benchmark]
    public long Adaptive() => adaptive.Plan(parcels, Fleet).TotalProfitHellers;
}

/// <summary>
/// How the planner scales with the number of parcels.
/// </summary>
/// <remarks>
/// The same demand mix throughout; only the parcel count changes.
/// </remarks>
[MemoryDiagnoser]
public class ScalingBenchmarks
{
    private static readonly FleetConfiguration Fleet = FleetConfiguration.Default;

    private Parcel[] parcels = [];
    private AdaptivePlanner planner = null!;

    [Params(100_000, 300_000, 600_000, 1_200_000)]
    public int ParcelCount { get; set; } = 300_000;

    [GlobalSetup]
    public void Setup()
    {
        parcels = ParcelGenerator.Generate(
            DemandMix.ByName("OrdinaryDay").WithParcelCount(ParcelCount), seed: 1);
        planner = new AdaptivePlanner(new BalancedVanSelection());
    }

    [Benchmark]
    public long Plan() => planner.Plan(parcels, Fleet).TotalProfitHellers;
}

/// <summary>
/// Where the time inside a plan goes: ordering the pool against loading the vans.
/// </summary>
[MemoryDiagnoser]
public class PlanStageBenchmarks
{
    private static readonly FleetConfiguration Fleet = FleetConfiguration.Default;

    private Parcel[] parcels = [];
    private ProfitDensityRanking ranking = null!;
    private readonly IVanSelection balanced = new BalancedVanSelection();

    [GlobalSetup]
    public void Setup()
    {
        parcels = ParcelGenerator.Generate(DemandMix.ByName("OrdinaryDay"), seed: 1);
        ranking = new ProfitDensityRanking(parcels.Length);
    }

    /// <summary>
    /// Ordering by profit density, without comparing any two parcels.
    /// </summary>
    [Benchmark]
    public int Ranking()
    {
        ranking.Rank(parcels, ResourcePrice.Balanced(Fleet));
        return ranking.Order.Length;
    }

    /// <summary>
    /// A comparison sort over the same keys, for reference.
    /// </summary>
    [Benchmark]
    public int SortForComparison()
    {
        double[] keys = new double[parcels.Length];
        int[] order = new int[parcels.Length];
        ResourcePrice price = ResourcePrice.Balanced(Fleet);

        for (int i = 0; i < parcels.Length; i++)
        {
            keys[i] = -price.ScoreOf(parcels[i]);
            order[i] = i;
        }

        Array.Sort(keys, order);
        return order.Length;
    }

    /// <summary>
    /// Loading the fleet, choosing the van that stays most evenly loaded.
    /// </summary>
    [Benchmark]
    public int BalancedPacking() => Pack(balanced);

    private int Pack(IVanSelection selection)
    {
        ranking.Rank(parcels, ResourcePrice.Balanced(Fleet));

        VanLoad[] vans = FleetPacking.EmptyFleet(Fleet);
        List<int> rejected = [];
        FleetPacking.LoadInOrder(vans, parcels, ranking.Order, selection, rejected);

        return rejected.Count;
    }
}
