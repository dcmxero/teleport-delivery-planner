using System.Diagnostics;
using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// Plans one circuit, choosing the weight and volume prices to suit the day's demand.
/// </summary>
/// <remarks>
/// Tries a spread of price splits, estimates each cheaply and builds full plans for the
/// best few. The fixed-price plan is always built alongside them, so it never earns less.
/// </remarks>
/// <param name="vanSelection">How a van is chosen for a parcel.</param>
/// <param name="options">Tuning, or <see cref="AdaptivePlannerOptions.Default"/> when null.</param>
public sealed class AdaptivePlanner(
    IVanSelection vanSelection,
    AdaptivePlannerOptions? options = null) : IDeliveryPlanner
{
    // Not named options: methods would read the parameter, not this field.
    private readonly AdaptivePlannerOptions settings =
        (options ?? AdaptivePlannerOptions.Default).Validated();

    /// <summary>
    /// How many candidates are estimated at once.
    /// </summary>
    private const int EstimateWorkers = 4;

    /// <summary>
    /// Name of the planner and its loading rule.
    /// </summary>
    public string Name => $"adaptive/{vanSelection.Name}";

    /// <summary>
    /// Builds a plan for one circuit.
    /// </summary>
    /// <param name="parcels">Everything waiting in the warehouse.</param>
    /// <param name="fleet">The vans available for this circuit.</param>
    public DeliveryPlan Plan(IReadOnlyList<Parcel> parcels, FleetConfiguration fleet)
    {
        ArgumentNullException.ThrowIfNull(parcels);

        Stopwatch elapsed = Stopwatch.StartNew();

        // If everything fits, there is nothing to choose.
        Parcel[] candidates = [.. parcels];
        long totalWeight = 0;
        long totalVolume = 0;

        foreach (Parcel parcel in candidates)
        {
            totalWeight += parcel.WeightGrams;
            totalVolume += parcel.VolumeCm3;
        }

        if (totalWeight <= fleet.TotalWeightCapacityGrams
            && totalVolume <= fleet.TotalVolumeCapacityCm3
            && WholePoolLoading.TryLoadEverything(candidates, fleet, vanSelection)
                is { } everything)
        {
            return new DeliveryPlan(
                fleet,
                everything,
                rejectedParcelIds: [],
                new PlanningTrace(
                    UsedFastPath: true,
                    CandidatesEstimated: 0,
                    PlansBuilt: 1,
                    ChosenTheta: null,
                    Elapsed: elapsed.Elapsed));
        }

        return Choose(candidates, fleet, elapsed);
    }

    /// <summary>
    /// Searches for prices that suit the demand, then builds plans for the best of them.
    /// </summary>
    private DeliveryPlan Choose(
        Parcel[] candidates,
        FleetConfiguration fleet,
        Stopwatch elapsed)
    {
        double[] thetas = new double[settings.PriceCandidates];
        long[] estimates = new long[settings.PriceCandidates];

        for (int i = 0; i < thetas.Length; i++)
        {
            thetas[i] = thetas.Length == 1 ? 0.5 : (double)i / (thetas.Length - 1);
        }

        // Four workers only: each holds its own ranking arrays, about 9 MB at a million
        // parcels, and loading the fleet costs far more than estimating anyway.
        ParallelOptions estimating = new()
        {
            MaxDegreeOfParallelism = EstimateWorkers,
        };

        int estimated = 0;

        Parallel.For(
            0,
            thetas.Length,
            estimating,
            () => new ProfitDensityRanking(candidates.Length),
            (i, _, ranking) =>
            {
                // Past the budget, skip: an estimate of zero sorts last.
                if (elapsed.Elapsed <= settings.TimeBudget)
                {
                    ranking.Rank(candidates, ResourcePrice.Split(fleet, thetas[i]));
                    estimates[i] = EstimateProfit(candidates, ranking.Order, fleet);
                    Interlocked.Increment(ref estimated);
                }

                return ranking;
            },
            static _ => { });

        int[] finalists = PickFinalists(thetas, estimates, elapsed);

        // Built in parallel, because loading is most of the time (about 75 ms of an 80 ms
        // plan). Each finalist has its own ranking, whose arrays would otherwise clash.
        // The last slot is the fixed-price plan, built even when the budget has run out.
        GreedyPlanner fixedPrices = new(ResourcePrice.Balanced(fleet), vanSelection);
        (IReadOnlyList<VanLoad> Vans, IReadOnlyList<int> Rejected, long Profit)[] built =
            new (IReadOnlyList<VanLoad>, IReadOnlyList<int>, long)[finalists.Length + 1];

        Parallel.For(0, built.Length, index =>
        {
            if (index == finalists.Length)
            {
                DeliveryPlan plan = fixedPrices.Plan(candidates, fleet);
                built[index] = (plan.Vans, plan.RejectedParcelIds, plan.TotalProfitHellers);
                return;
            }

            ProfitDensityRanking finalistRanking = new(candidates.Length);
            finalistRanking.Rank(candidates, ResourcePrice.Split(fleet, thetas[finalists[index]]));

            VanLoad[] vans = FleetPacking.EmptyFleet(fleet);
            List<int> rejected = [];
            FleetPacking.LoadInOrder(
                vans, candidates, finalistRanking.Order, vanSelection, rejected);

            built[index] = (vans, rejected, vans.Sum(van => van.ProfitHellers));
        });

        int winner = 0;
        for (int index = 1; index < built.Length; index++)
        {
            if (built[index].Profit > built[winner].Profit)
            {
                winner = index;
            }
        }

        // The fixed prices are the even split.
        double bestTheta = winner == finalists.Length ? 0.5 : thetas[finalists[winner]];
        int plansBuilt = built.Length;

        return new DeliveryPlan(
            fleet,
            built[winner].Vans,
            built[winner].Rejected,
            new PlanningTrace(
                UsedFastPath: false,
                CandidatesEstimated: estimated,
                PlansBuilt: plansBuilt,
                ChosenTheta: bestTheta,
                Elapsed: elapsed.Elapsed));
    }

    /// <summary>
    /// Estimates what a set of prices would earn, filling the fleet as one pooled van.
    /// </summary>
    /// <remarks>
    /// Overstates every candidate, but it only serves to compare them.
    /// </remarks>
    private static long EstimateProfit(
        Parcel[] candidates,
        ReadOnlySpan<int> order,
        FleetConfiguration fleet)
    {
        long remainingWeight = fleet.TotalWeightCapacityGrams;
        long remainingVolume = fleet.TotalVolumeCapacityCm3;
        long profit = 0;

        foreach (int position in order)
        {
            ref Parcel parcel = ref candidates[position];

            if (parcel.WeightGrams > remainingWeight || parcel.VolumeCm3 > remainingVolume)
            {
                continue;
            }

            remainingWeight -= parcel.WeightGrams;
            remainingVolume -= parcel.VolumeCm3;
            profit += parcel.ProfitHellers;
        }

        return profit;
    }

    /// <summary>
    /// Chooses which candidates deserve a full plan.
    /// </summary>
    /// <remarks>
    /// The best estimates, plus the best of each end of the range: the estimate cannot see
    /// how a lopsided selection packs into single vans. An end that is also among the best
    /// takes no second slot, so there can be fewer than <c>Finalists</c>.
    /// </remarks>
    private int[] PickFinalists(double[] thetas, long[] estimates, Stopwatch elapsed)
    {
        // Budget spent: one finalist, beside the fixed-price plan.
        int room = elapsed.Elapsed >= settings.TimeBudget ? 1 : settings.Finalists;

        // Nothing estimated: the fixed-price plan, built anyway, is all there is.
        if (estimates.All(estimate => estimate == 0))
        {
            return [];
        }

        List<int> chosen = [];

        foreach (int candidate in Enumerable.Range(0, thetas.Length)
                     .OrderByDescending(i => estimates[i])
                     .Take(Math.Max(1, room - 2)))
        {
            chosen.Add(candidate);
        }

        if (room > 1)
        {
            AddBestWithin(chosen, thetas, estimates, from: 0.0, to: 0.25);
            AddBestWithin(chosen, thetas, estimates, from: 0.75, to: 1.0);
        }

        // The two end candidates can push the list past the limit.
        return [.. chosen.OrderByDescending(i => estimates[i]).Take(room)];
    }

    private static void AddBestWithin(
        List<int> chosen,
        double[] thetas,
        long[] estimates,
        double from,
        double to)
    {
        int best = -1;

        for (int i = 0; i < thetas.Length; i++)
        {
            if (thetas[i] >= from && thetas[i] <= to
                && (best < 0 || estimates[i] > estimates[best]))
            {
                best = i;
            }
        }

        if (best >= 0 && !chosen.Contains(best))
        {
            chosen.Add(best);
        }
    }
}

