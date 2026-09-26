using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// Loading a whole pool, for the days when nothing has to be left behind.
/// </summary>
public static class WholePoolLoading
{
    /// <summary>
    /// Loads the whole pool, or returns null when some parcel does not fit.
    /// </summary>
    /// <remarks>
    /// Fitting the fleet's total is not enough: two 10 kg vans cannot take three 6 kg
    /// parcels. So arrival order is tried first, then largest first.
    /// </remarks>
    public static VanLoad[]? TryLoadEverything(
        Parcel[] candidates,
        FleetConfiguration fleet,
        IVanSelection vanSelection)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        int count = candidates.Length;
        int[] arrivalOrder = new int[count];
        for (int i = 0; i < count; i++)
        {
            arrivalOrder[i] = i;
        }

        if (TryLoadAll(candidates, arrivalOrder, fleet, vanSelection)
            is { } asTheyCame)
        {
            return asTheyCame;
        }

        // Largest first: big parcels go in while the vans are empty, small ones fill gaps.
        double[] sizeKeys = new double[count];
        for (int i = 0; i < count; i++)
        {
            sizeKeys[i] = -Math.Max(
                (double)candidates[i].WeightGrams / fleet.VanWeightCapacityGrams,
                (double)candidates[i].VolumeCm3 / fleet.VanVolumeCapacityCm3);
        }

        int[] largestFirst = new int[count];
        for (int i = 0; i < count; i++)
        {
            largestFirst[i] = i;
        }

        Array.Sort(sizeKeys, largestFirst);

        return TryLoadAll(candidates, largestFirst, fleet, vanSelection);
    }

    /// <summary>
    /// Loads every parcel in the given order, returning null if any of them will not fit.
    /// </summary>
    private static VanLoad[]? TryLoadAll(
        Parcel[] candidates,
        int[] order,
        FleetConfiguration fleet,
        IVanSelection vanSelection)
    {
        VanLoad[] vans = FleetPacking.EmptyFleet(fleet);
        List<int> rejected = [];

        FleetPacking.LoadInOrder(vans, candidates, order, vanSelection, rejected);

        return rejected.Count == 0 ? vans : null;
    }
}
