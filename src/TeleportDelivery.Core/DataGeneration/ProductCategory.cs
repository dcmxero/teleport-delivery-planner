namespace TeleportDelivery.Core.DataGeneration;

/// <summary>
/// One kind of goods, described by what a typical item of it looks like.
/// </summary>
/// <remarks>
/// Parcels are built from goods rather than from bare distributions, so their weight,
/// volume and profit hang together and can be checked against real items.
/// </remarks>
/// <param name="Name">What the category is called.</param>
/// <param name="TypicalVolumeCm3">Volume of a middling item of this kind.</param>
/// <param name="TypicalWeightGrams">Weight of a middling item of this kind.</param>
/// <param name="TypicalPriceCrowns">What a middling item of this kind sells for.</param>
/// <param name="MarginRate">Share of the selling price the retailer keeps.</param>
/// <param name="ArticleCount">How many distinct articles of this kind the assortment holds.</param>
/// <param name="Spread">
/// How much items within the category vary: about 0.35 is tight, 0.8 wide.
/// </param>
public readonly record struct ProductCategory(
    string Name,
    double TypicalVolumeCm3,
    double TypicalWeightGrams,
    double TypicalPriceCrowns,
    double MarginRate,
    int ArticleCount,
    double Spread)
{
    /// <summary>
    /// The assortment the catalogue is built from.
    /// </summary>
    /// <remarks>
    /// Estimates of packed goods, not any retailer's data. Article counts keep the ratios
    /// of one Czech retailer's published ranges, scaled down; margins are thin on
    /// electronics and groceries, wide on clothing and homeware.
    /// </remarks>
    public static IReadOnlyList<ProductCategory> All { get; } =
    [
        new("Books",              1_200,    450,    350, 0.20, 400, 0.55),
        new("Stationery",         2_500,    900,    250, 0.25, 240, 0.60),
        new("PhoneAccessories",     800,    300,    500, 0.35, 700, 0.70),
        new("Laptops",            6_000,  1_800, 22_000, 0.05, 520, 0.40),
        new("GamingConsoles",     5_000,  3_000, 12_000, 0.03,  12, 0.35),
        new("Cameras",            2_200,    900, 15_000, 0.09,  90, 0.55),
        new("Watches",              300,    150,  2_600, 0.22, 170, 0.55),
        new("Cosmetics",            900,    350,    450, 0.26, 340, 0.60),
        new("Pharmacy",             700,    250,    320, 0.20, 190, 0.55),
        new("SmallAppliances",   12_000,  2_500,  2_200, 0.13, 380, 0.65),
        new("LargeAppliances",   60_000,  9_000, 12_000, 0.09, 330, 0.55),
        new("PowerTools",        18_000,  4_500,  3_500, 0.17, 160, 0.60),
        new("HandTools",          3_000,  1_500,    700, 0.24, 250, 0.70),
        new("Batteries",            600,    700,    700, 0.24,  70, 0.50),
        new("Beverages",          8_000,  9_000,    600, 0.07, 100, 0.45),
        new("PetSupplies",       20_000,  5_000,    700, 0.20, 230, 0.75),
        new("Clothing",           4_000,    400,    800, 0.28, 520, 0.60),
        new("Footwear",           9_000,    900,  1_600, 0.28, 200, 0.45),
        new("Bedding",           45_000,  1_200,  1_500, 0.28, 140, 0.55),
        new("PaperGoods",        40_000,  1_500,    400, 0.12, 110, 0.50),
        new("HomeDecor",         14_000,  1_800,    900, 0.30, 420, 0.80),
        new("Garden",            50_000,  7_000,  2_500, 0.22, 270, 0.85),
        new("SportsEquipment",   30_000,  2_500,  2_400, 0.24, 300, 0.80),
        new("Toys",              15_000,    800,    700, 0.24, 380, 0.75),
        new("BabyProducts",      25_000,  2_000,  1_200, 0.20, 210, 0.70),
        new("CarAccessories",    10_000,  3_500,    900, 0.24, 290, 0.75),

        // Denser than a van's 0.79 kg per litre, so weight can be the binding limit.
        new("Fitness",           10_000, 14_000,  1_800, 0.25, 120, 0.70),
        new("PetFood",           28_000, 14_000,    900, 0.12,  80, 0.55),
        new("PaintChemicals",     6_000,  7_000,    800, 0.20,  95, 0.60),
        new("CarBatteries",       8_000, 16_000,  2_800, 0.15,  55, 0.40),
    ];
}
