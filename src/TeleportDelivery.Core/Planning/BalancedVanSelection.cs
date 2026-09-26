using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// Puts each parcel into the van that stays most evenly loaded afterwards.
/// </summary>
/// <remarks>
/// Scored on the tighter of the two limits, so no van fills on one while the other stays
/// empty. Scans all vans; a heap cannot order vans by two limits at once.
/// </remarks>
public sealed class BalancedVanSelection : IVanSelection
{
    /// <inheritdoc/>
    public string Name => "balanced";

    /// <inheritdoc/>
    public int SelectVan(VanLoad[] vans, in Parcel parcel)
    {
        ArgumentNullException.ThrowIfNull(vans);

        int best = -1;
        double bestLoad = double.MaxValue;

        for (int i = 0; i < vans.Length; i++)
        {
            VanLoad van = vans[i];

            if (!van.CanFit(parcel))
            {
                continue;
            }

            double weightLoad =
                (double)(van.UsedWeightGrams + parcel.WeightGrams) / van.WeightCapacityGrams;
            double volumeLoad =
                (double)(van.UsedVolumeCm3 + parcel.VolumeCm3) / van.VolumeCapacityCm3;
            double load = Math.Max(weightLoad, volumeLoad);

            if (load < bestLoad)
            {
                bestLoad = load;
                best = i;
            }
        }

        return best;
    }
}
