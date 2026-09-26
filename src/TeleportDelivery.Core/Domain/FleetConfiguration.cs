namespace TeleportDelivery.Core.Domain;

/// <summary>
/// The fleet available for one circuit: how many vans there are and what each one holds.
/// </summary>
/// <param name="VanCount">Number of vans available for this circuit.</param>
/// <param name="VanWeightCapacityGrams">Maximum load weight of a single van, in grams.</param>
/// <param name="VanVolumeCapacityCm3">
/// Maximum load volume of a single van, in cubic centimetres.
/// </param>
public readonly record struct FleetConfiguration(
    int VanCount,
    int VanWeightCapacityGrams,
    int VanVolumeCapacityCm3)
{
    /// <summary>
    /// The fleet described by the assignment: 120 vans, 5.5 tonnes and 7 m3 each.
    /// </summary>
    public static FleetConfiguration Default { get; } = new(
        VanCount: 120,
        VanWeightCapacityGrams: 5_500_000,
        VanVolumeCapacityCm3: 7_000_000);

    /// <summary>
    /// Total weight the whole fleet can carry on one circuit.
    /// </summary>
    public long TotalWeightCapacityGrams => (long)VanCount * VanWeightCapacityGrams;

    /// <summary>
    /// Total volume the whole fleet can carry on one circuit.
    /// </summary>
    public long TotalVolumeCapacityCm3 => (long)VanCount * VanVolumeCapacityCm3;

    /// <summary>
    /// Whether the parcel fits an empty van.
    /// </summary>
    public bool CanEverCarry(in Parcel parcel) =>
        parcel.WeightGrams <= VanWeightCapacityGrams &&
        parcel.VolumeCm3 <= VanVolumeCapacityCm3;
}
