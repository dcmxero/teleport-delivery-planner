namespace TeleportDelivery.Core.Domain;

/// <summary>
/// A single parcel waiting to be dispatched.
/// </summary>
/// <remarks>
/// Integer units (grams, cm3, hellers) keep capacity checks exact.
/// </remarks>
/// <param name="Id">Identifier, unique within one planning run.</param>
/// <param name="WeightGrams">Weight in grams.</param>
/// <param name="VolumeCm3">Volume in cubic centimetres.</param>
/// <param name="ProfitHellers">Profit in hellers, i.e. hundredths of a crown.</param>
public readonly record struct Parcel(
    int Id,
    int WeightGrams,
    int VolumeCm3,
    long ProfitHellers);
