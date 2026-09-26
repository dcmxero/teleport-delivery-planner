using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// Loading parcels into vans, shared by every planner.
/// </summary>
public static class FleetPacking
{
    /// <summary>
    /// An empty van for every vehicle in the fleet.
    /// </summary>
    public static VanLoad[] EmptyFleet(FleetConfiguration fleet)
    {
        VanLoad[] vans = new VanLoad[fleet.VanCount];

        for (int i = 0; i < vans.Length; i++)
        {
            vans[i] = new VanLoad(i, fleet.VanWeightCapacityGrams, fleet.VanVolumeCapacityCm3);
        }

        return vans;
    }

    /// <summary>
    /// Loads the parcels in the given order, collecting the ones that fit nowhere.
    /// </summary>
    /// <remarks>
    /// A parcel that does not fit does not end the walk; smaller ones later still can.
    /// </remarks>
    /// <param name="vans">The fleet to load into.</param>
    /// <param name="parcels">The pool, indexed by the entries of <paramref name="order"/>.</param>
    /// <param name="order">Positions into <paramref name="parcels"/>, best candidate first.</param>
    /// <param name="vanSelection">How a van is chosen for a parcel.</param>
    /// <param name="rejected">Receives the identifiers of parcels that fit nowhere.</param>
    public static void LoadInOrder(
        VanLoad[] vans,
        Parcel[] parcels,
        ReadOnlySpan<int> order,
        IVanSelection vanSelection,
        List<int> rejected)
    {
        ArgumentNullException.ThrowIfNull(vans);
        ArgumentNullException.ThrowIfNull(parcels);
        ArgumentNullException.ThrowIfNull(vanSelection);
        ArgumentNullException.ThrowIfNull(rejected);

        foreach (int position in order)
        {
            ref Parcel parcel = ref parcels[position];
            int van = vanSelection.SelectVan(vans, parcel);

            if (van < 0)
            {
                rejected.Add(parcel.Id);
                continue;
            }

            vans[van].Add(parcel);
        }
    }
}
