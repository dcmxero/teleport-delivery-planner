using System.Globalization;

namespace TeleportDelivery.Core.DataGeneration;

/// <summary>
/// The fixed assortment every generated parcel pool is drawn from.
/// </summary>
/// <remarks>
/// Built from a fixed seed on every run; <c>data/catalogue.csv</c> is a readable copy.
/// Articles vary log-normally around their category's typical figures.
/// </remarks>
public static class ProductCatalogue
{
    /// <summary>
    /// Changing it changes every measurement in the repository.
    /// </summary>
    private const ulong CatalogueSeed = 20260924;

    private static readonly Lazy<Product[]> Articles = new(Build);

    /// <summary>
    /// Every article, in catalogue order.
    /// </summary>
    public static ReadOnlySpan<Product> All => Articles.Value;

    /// <summary>
    /// The articles of one category.
    /// </summary>
    public static Product[] InCategory(string category) =>
        [.. Articles.Value.Where(
            product => product.Category.Equals(category, StringComparison.OrdinalIgnoreCase))];

    private static Product[] Build()
    {
        // Separate streams, so size and margin are not correlated by accident.
        Pcg32 sizeRandom = new(CatalogueSeed, sequence: 11);
        Pcg32 marginRandom = new(CatalogueSeed, sequence: 12);

        List<Product> products = new(ProductCategory.All.Sum(category => category.ArticleCount));
        int id = 0;

        foreach (ProductCategory category in ProductCategory.All)
        {
            for (int i = 0; i < category.ArticleCount; i++)
            {
                double sizeDraw = sizeRandom.NextGaussian();

                // Weight follows size closely, price only loosely.
                double weightDraw = (0.75 * sizeDraw) + (0.66 * sizeRandom.NextGaussian());

                double priceDraw = (0.25 * sizeDraw) + (0.97 * marginRandom.NextGaussian());

                long price = Quantise(
                    category.TypicalPriceCrowns * 100.0, category.Spread * 0.9, priceDraw);

                // A little variation around the category's margin.
                double margin = category.MarginRate
                    * Math.Exp(0.18 * marginRandom.NextGaussian());

                products.Add(new Product(
                    Id: id++,
                    Category: category.Name,
                    VolumeCm3: Quantise(category.TypicalVolumeCm3, category.Spread, sizeDraw),
                    WeightGrams: Quantise(category.TypicalWeightGrams, category.Spread, weightDraw),
                    PriceHellers: price,
                    ProfitHellers: Math.Max(1L, (long)Math.Round(price * margin))));
            }
        }

        return [.. products];
    }

    /// <summary>
    /// Scales a typical figure by a log-normal draw, rounded and at least one.
    /// </summary>
    private static int Quantise(double typical, double spread, double draw) =>
        (int)Math.Clamp(Math.Round(typical * Math.Exp(spread * draw)), 1.0, int.MaxValue);

    private const string Header =
        "id,category,volume_cm3,weight_grams,price_hellers,profit_hellers";

    /// <summary>
    /// Writes the catalogue as CSV.
    /// </summary>
    public static void Write(string path)
    {
        using StreamWriter writer = new(path);
        writer.Write(Header);
        writer.Write('\n');

        foreach (Product product in Articles.Value)
        {
            writer.Write(product.Id);
            writer.Write(',');
            writer.Write(product.Category);
            writer.Write(',');
            writer.Write(product.VolumeCm3);
            writer.Write(',');
            writer.Write(product.WeightGrams);
            writer.Write(',');
            writer.Write(product.PriceHellers);
            writer.Write(',');
            writer.Write(product.ProfitHellers);
            writer.Write('\n');
        }
    }

    /// <summary>
    /// Reads a catalogue CSV back.
    /// </summary>
    public static IReadOnlyList<Product> Read(string path)
    {
        List<Product> products = [];

        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0 || line.StartsWith("id,", StringComparison.Ordinal))
            {
                continue;
            }

            string[] fields = line.Split(',');

            products.Add(new Product(
                Id: int.Parse(fields[0], CultureInfo.InvariantCulture),
                Category: fields[1],
                VolumeCm3: int.Parse(fields[2], CultureInfo.InvariantCulture),
                WeightGrams: int.Parse(fields[3], CultureInfo.InvariantCulture),
                PriceHellers: long.Parse(fields[4], CultureInfo.InvariantCulture),
                ProfitHellers: long.Parse(fields[5], CultureInfo.InvariantCulture)));
        }

        return products;
    }
}
