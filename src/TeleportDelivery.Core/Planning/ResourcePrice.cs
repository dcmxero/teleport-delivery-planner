using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// What a gram of weight and a cubic centimetre of volume cost when ranking parcels.
/// </summary>
/// <remarks>
/// Pricing both limits covers volume-bound days, weight-bound days and everything between.
/// </remarks>
/// <param name="PerGram">Cost charged for one gram of parcel weight.</param>
/// <param name="PerCm3">Cost charged for one cubic centimetre of parcel volume.</param>
public readonly record struct ResourcePrice(double PerGram, double PerCm3)
{
    /// <summary>
    /// Fleet weight and fleet volume cost the same; the fixed price of the baseline.
    /// </summary>
    /// <param name="fleet">The fleet whose capacities the prices are expressed against.</param>
    public static ResourcePrice Balanced(FleetConfiguration fleet) => new(
        PerGram: 1.0 / fleet.TotalWeightCapacityGrams,
        PerCm3: 1.0 / fleet.TotalVolumeCapacityCm3);

    /// <summary>
    /// Splits a unit of cost, charging <paramref name="theta"/> of it to weight.
    /// </summary>
    /// <remarks>
    /// 0 prices only volume, 1 only weight, 0.5 equals <see cref="Balanced"/>.
    /// </remarks>
    /// <param name="fleet">The fleet whose capacities the prices are expressed against.</param>
    /// <param name="theta">Share of cost charged to weight, from 0 to 1.</param>
    public static ResourcePrice Split(FleetConfiguration fleet, double theta) => new(
        PerGram: theta / fleet.TotalWeightCapacityGrams,
        PerCm3: (1.0 - theta) / fleet.TotalVolumeCapacityCm3);

    /// <summary>
    /// What the parcel costs to carry at these prices.
    /// </summary>
    public double CostOf(in Parcel parcel) =>
        (PerGram * parcel.WeightGrams) + (PerCm3 * parcel.VolumeCm3);

    /// <summary>
    /// Profit per unit of cost; parcels are loaded in descending order of this.
    /// </summary>
    public double ScoreOf(in Parcel parcel) => parcel.ProfitHellers / CostOf(parcel);
}
