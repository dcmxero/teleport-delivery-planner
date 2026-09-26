namespace TeleportDelivery.Core.DataGeneration;

/// <summary>
/// One article a retailer stocks, with the dimensions and margin it carries.
/// </summary>
/// <param name="Id">Position in the catalogue.</param>
/// <param name="Category">Which kind of goods it belongs to.</param>
/// <param name="VolumeCm3">Volume of the article as it travels, in cubic centimetres.</param>
/// <param name="WeightGrams">Weight of the article as it travels, in grams.</param>
/// <param name="PriceHellers">What it sells for, in hellers.</param>
/// <param name="ProfitHellers">
/// What the retailer keeps of that price, in hellers; this, not the price, is maximised.
/// </param>
public readonly record struct Product(
    int Id,
    string Category,
    int VolumeCm3,
    int WeightGrams,
    long PriceHellers,
    long ProfitHellers);
