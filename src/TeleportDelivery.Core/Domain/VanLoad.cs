namespace TeleportDelivery.Core.Domain;

/// <summary>
/// What one van is carrying.
/// </summary>
/// <remarks>
/// Mutable, because its totals change on every one of hundreds of thousands of loads.
/// </remarks>
/// <param name="index">Position of this van in the fleet.</param>
/// <param name="weightCapacityGrams">Maximum load weight, in grams.</param>
/// <param name="volumeCapacityCm3">Maximum load volume, in cubic centimetres.</param>
public sealed class VanLoad(int index, int weightCapacityGrams, int volumeCapacityCm3)
{
    private readonly List<int> parcelIds = [];

    /// <summary>
    /// Position of this van in the fleet.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    /// Maximum load weight of this van, in grams.
    /// </summary>
    public int WeightCapacityGrams { get; } = weightCapacityGrams;

    /// <summary>
    /// Maximum load volume of this van, in cubic centimetres.
    /// </summary>
    public int VolumeCapacityCm3 { get; } = volumeCapacityCm3;

    /// <summary>
    /// Weight already loaded, in grams.
    /// </summary>
    public int UsedWeightGrams { get; private set; }

    /// <summary>
    /// Volume already loaded, in cubic centimetres.
    /// </summary>
    public int UsedVolumeCm3 { get; private set; }

    /// <summary>
    /// Profit carried by the parcels loaded so far, in hellers.
    /// </summary>
    public long ProfitHellers { get; private set; }

    /// <summary>
    /// Identifiers of the parcels loaded into this van, in the order they were assigned.
    /// </summary>
    public IReadOnlyList<int> ParcelIds => parcelIds;

    /// <summary>
    /// Weight this van can still take, in grams.
    /// </summary>
    public int RemainingWeightGrams => WeightCapacityGrams - UsedWeightGrams;

    /// <summary>
    /// Volume this van can still take, in cubic centimetres.
    /// </summary>
    public int RemainingVolumeCm3 => VolumeCapacityCm3 - UsedVolumeCm3;

    /// <summary>
    /// Whether the parcel still fits, on both weight and volume.
    /// </summary>
    public bool CanFit(in Parcel parcel) =>
        parcel.WeightGrams <= RemainingWeightGrams &&
        parcel.VolumeCm3 <= RemainingVolumeCm3;

    /// <summary>
    /// Loads a parcel and updates the running totals.
    /// </summary>
    /// <exception cref="InvalidOperationException">The parcel does not fit.</exception>
    public void Add(in Parcel parcel)
    {
        if (!CanFit(parcel))
        {
            throw new InvalidOperationException(
                $"Parcel {parcel.Id} does not fit into van {Index}.");
        }

        UsedWeightGrams += parcel.WeightGrams;
        UsedVolumeCm3 += parcel.VolumeCm3;
        ProfitHellers += parcel.ProfitHellers;
        parcelIds.Add(parcel.Id);
    }
}
