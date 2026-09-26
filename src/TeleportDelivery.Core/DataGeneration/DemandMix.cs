namespace TeleportDelivery.Core.DataGeneration;

/// <summary>
/// A day's worth of demand: how many parcels there are and what is in them.
/// </summary>
/// <remarks>
/// Which limit runs out first follows from what was ordered; it is not set directly.
/// </remarks>
/// <param name="Name">ASCII name used in arguments, files and tests.</param>
/// <param name="ParcelCount">How many parcels are waiting.</param>
/// <param name="Weights">Relative share of each category; only the ratios matter.</param>
/// <param name="Note">What the day represents, for the reader of the source.</param>
public sealed record DemandMix(
    string Name,
    int ParcelCount,
    IReadOnlyDictionary<string, double> Weights,
    string Note)
{
    /// <summary>
    /// An everyday assortment: a bit of everything.
    /// </summary>
    private static readonly Dictionary<string, double> Everyday = new()
    {
        ["Books"] = 2.0,
        ["Stationery"] = 1.0,
        ["PhoneAccessories"] = 2.0,
        ["Laptops"] = 0.8,
        ["GamingConsoles"] = 0.5,
        ["Cameras"] = 0.4,
        ["Watches"] = 0.4,
        ["Cosmetics"] = 1.5,
        ["Pharmacy"] = 1.2,
        ["SmallAppliances"] = 1.2,
        ["LargeAppliances"] = 0.4,
        ["PowerTools"] = 0.6,
        ["HandTools"] = 0.8,
        ["Batteries"] = 1.0,
        ["Beverages"] = 1.0,
        ["PetSupplies"] = 0.8,
        ["Clothing"] = 2.5,
        ["Footwear"] = 1.2,
        ["Bedding"] = 0.6,
        ["PaperGoods"] = 0.8,
        ["HomeDecor"] = 1.0,
        ["Garden"] = 0.5,
        ["SportsEquipment"] = 0.6,
        ["Toys"] = 1.2,
        ["BabyProducts"] = 0.8,
        ["CarAccessories"] = 0.8,
    };

    /// <summary>
    /// The ten days the planner is measured against.
    /// </summary>
    /// <remarks>
    /// From a day where everything fits to a million-plus peak, including deliberately
    /// extreme assortments.
    /// </remarks>
    public static IReadOnlyList<DemandMix> All { get; } =
    [
        new("QuietDay", 100_000, new Dictionary<string, double>
        {
            ["Books"] = 3.0,
            ["Cosmetics"] = 2.5,
            ["Pharmacy"] = 2.5,
            ["PhoneAccessories"] = 2.0,
            ["Stationery"] = 1.5,
            ["Clothing"] = 1.0,
            ["Watches"] = 0.3,
        }, "A slow day of small goods. Demand sits inside what the fleet can carry, so "
            + "nothing needs choosing and everything should travel."),

        new("OrdinaryDay", 300_000, Everyday,
            "Normal trading across the whole assortment. Demand runs past capacity, so "
            + "parcels have to be left behind."),

        new("BusyDay", 600_000, Everyday,
            "A heavy day on the same assortment. Twice the parcels, the same goods."),

        new("PeakSeason", 1_200_000, new Dictionary<string, double>
        {
            ["Toys"] = 3.0,
            ["GamingConsoles"] = 2.0,
            ["Clothing"] = 2.5,
            ["Footwear"] = 1.5,
            ["Cosmetics"] = 2.0,
            ["Books"] = 1.5,
            ["PhoneAccessories"] = 1.5,
            ["Watches"] = 0.4,
            ["HomeDecor"] = 1.0,
            ["SmallAppliances"] = 1.0,
        }, "Christmas. Far more parcels than any other day, skewed towards gifts."),

        new("BulkyGoods", 300_000, new Dictionary<string, double>
        {
            ["Bedding"] = 3.0,
            ["PaperGoods"] = 3.0,
            ["Garden"] = 2.0,
            ["SportsEquipment"] = 1.5,
            ["BabyProducts"] = 1.5,
            ["Toys"] = 1.0,
            ["HomeDecor"] = 1.0,
        }, "Duvets, kitchen roll and garden furniture. Room runs out while the vans are "
            + "still light."),

        new("DenseGoods", 300_000, new Dictionary<string, double>
        {
            ["CarBatteries"] = 3.0,
            ["Fitness"] = 2.5,
            ["PaintChemicals"] = 2.5,
            ["Beverages"] = 2.0,
            ["Batteries"] = 1.5,
            ["PowerTools"] = 1.0,
            ["HandTools"] = 1.0,
        }, "Car batteries, weights, paint and bottled drinks. Weight runs out first, and a "
            + "few per cent of the room goes unused behind it."),

        new("ElectronicsWeek", 1_000_000, new Dictionary<string, double>
        {
            ["PhoneAccessories"] = 3.0,
            ["Laptops"] = 2.0,
            ["Cameras"] = 1.5,
            ["GamingConsoles"] = 1.5,
            ["Watches"] = 0.5,
            ["Batteries"] = 1.0,
            ["Pharmacy"] = 1.0,
            ["Cosmetics"] = 1.0,
        }, "A promotion on electronics. Most of the money is in small parcels, so size "
            + "is a poor guide to what is worth carrying."),

        new("MixedExtremes", 300_000, new Dictionary<string, double>
        {
            ["Bedding"] = 1.0,
            ["PaperGoods"] = 1.0,
            ["CarBatteries"] = 4.0,
            ["Fitness"] = 3.0,
        }, "Nothing but duvets, kitchen roll, car batteries and weights. Some parcels "
            + "are bulky and weightless, the rest dense and small, and a van fills on "
            + "both limits only by taking some of each."),

        new("LowMargin", 300_000, new Dictionary<string, double>
        {
            ["Books"] = 3.0,
            ["PaperGoods"] = 2.0,
            ["Stationery"] = 2.0,
            ["Beverages"] = 2.0,
            ["Pharmacy"] = 1.0,
        }, "Thin margins across the board. Parcels are hard to tell apart, so the "
            + "decision at the cut-off is close to a coin toss."),

        new("Clearance", 500_000, new Dictionary<string, double>
        {
            ["Clothing"] = 3.0,
            ["Footwear"] = 2.0,
            ["Toys"] = 2.0,
            ["HomeDecor"] = 2.0,
            ["Books"] = 2.0,
            ["Stationery"] = 1.5,
            ["Cosmetics"] = 1.5,
            ["PetSupplies"] = 1.0,
            ["CarAccessories"] = 1.0,
            ["Garden"] = 1.0,
        }, "An end-of-season sale. A great many cheap parcels across a wide assortment."),
    ];

    /// <summary>
    /// Finds a mix by name, ignoring case.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">No mix goes by that name.</exception>
    public static DemandMix ByName(string name)
    {
        foreach (DemandMix mix in All)
        {
            if (mix.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return mix;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(name), name, "No such demand mix.");
    }

    /// <summary>
    /// The same assortment with a different number of parcels waiting.
    /// </summary>
    public DemandMix WithParcelCount(int parcelCount) => this with { ParcelCount = parcelCount };
}
