using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// A ceiling on the profit any plan could possibly carry.
/// </summary>
/// <remarks>
/// Lagrangian relaxation of one pooled van: valid for any non-negative prices, so the
/// tightest of a spread is reported. A plan at 98% of it is at least 98% of the optimum;
/// a plan well below it has not necessarily lost anything, because the ceiling can be loose.
/// </remarks>
public static class ProfitUpperBound
{
    /// <summary>
    /// Number of price splits to evaluate.
    /// </summary>
    private const int DefaultPriceCandidates = 13;

    /// <summary>
    /// Computes the tightest ceiling found across a spread of prices.
    /// </summary>
    /// <remarks>
    /// Parcels no van can carry are left out; their profit would only loosen the ceiling.
    /// </remarks>
    /// <param name="parcels">Everything waiting in the warehouse.</param>
    /// <param name="fleet">The vans available for this circuit.</param>
    /// <param name="priceCandidates">How many splits of cost to try.</param>
    /// <returns>Profit in hellers that no plan for this fleet can exceed.</returns>
    public static double Compute(
        IReadOnlyList<Parcel> parcels,
        FleetConfiguration fleet,
        int priceCandidates = DefaultPriceCandidates)
    {
        ArgumentNullException.ThrowIfNull(parcels);
        ArgumentOutOfRangeException.ThrowIfLessThan(priceCandidates, 1);

        Parcel[] carryable = [.. parcels.Where(parcel => fleet.CanEverCarry(parcel))];

        if (carryable.Length == 0)
        {
            return 0.0;
        }

        ProfitDensityRanking ranking = new(carryable.Length);
        double tightest = double.MaxValue;

        for (int i = 0; i < priceCandidates; i++)
        {
            double theta = priceCandidates == 1 ? 0.5 : (double)i / (priceCandidates - 1);
            ResourcePrice price = ResourcePrice.Split(fleet, theta);

            ranking.Rank(carryable, price);
            tightest = Math.Min(tightest, BoundAt(carryable, ranking.Order, price));
        }

        // The single-resource ceilings once more with an exact sort, which is a little
        // tighter than the bucketed ranking.
        tightest = Math.Min(tightest, ExactSingleResourceBound(
            carryable, fleet.TotalVolumeCapacityCm3, static parcel => parcel.VolumeCm3));
        tightest = Math.Min(tightest, ExactSingleResourceBound(
            carryable, fleet.TotalWeightCapacityGrams, static parcel => parcel.WeightGrams));

        return tightest;
    }

    /// <summary>
    /// The ceiling implied by one split of cost between the two resources.
    /// </summary>
    /// <remarks>
    /// The whole fleet costs exactly one at these prices. The multiplier is the score of the
    /// parcel at which the fleet fills; any other value would still give a valid ceiling.
    /// </remarks>
    private static double BoundAt(
        Parcel[] parcels,
        ReadOnlySpan<int> order,
        ResourcePrice price)
    {
        double consumed = 0.0;
        double multiplier = 0.0;

        foreach (int position in order)
        {
            ref Parcel parcel = ref parcels[position];
            double cost = price.CostOf(parcel);

            if (consumed + cost > 1.0)
            {
                multiplier = parcel.ProfitHellers / cost;
                break;
            }

            consumed += cost;
        }

        // Zero when the fleet never filled: the ceiling is then every parcel's profit.
        double bound = multiplier;

        // Over every parcel, not only the walked prefix: the ranking is not exact within a
        // bucket, and a missed surplus would make the ceiling too low.
        foreach (Parcel parcel in parcels)
        {
            double surplus = parcel.ProfitHellers - (multiplier * price.CostOf(parcel));

            if (surplus > 0.0)
            {
                bound += surplus;
            }
        }

        return bound;
    }

    /// <summary>
    /// The exact optimum of the divisible knapsack over one resource.
    /// </summary>
    /// <param name="parcels">Parcels that at least fit a van.</param>
    /// <param name="capacity">Pooled capacity of the whole fleet in that resource.</param>
    /// <param name="consumption">How much of the resource a parcel uses.</param>
    private static double ExactSingleResourceBound(
        Parcel[] parcels,
        long capacity,
        Func<Parcel, int> consumption)
    {
        double[] keys = new double[parcels.Length];
        Parcel[] ordered = [.. parcels];

        for (int i = 0; i < ordered.Length; i++)
        {
            // Negated, so the ascending sort puts the best parcel first.
            keys[i] = -(double)ordered[i].ProfitHellers / consumption(ordered[i]);
        }

        Array.Sort(keys, ordered);

        long remaining = capacity;
        double bound = 0.0;

        foreach (Parcel parcel in ordered)
        {
            int used = consumption(parcel);

            if (used <= remaining)
            {
                bound += parcel.ProfitHellers;
                remaining -= used;
                continue;
            }

            // The first parcel that does not fit is taken in part.
            bound += (double)remaining / used * parcel.ProfitHellers;
            break;
        }

        return bound;
    }
}
