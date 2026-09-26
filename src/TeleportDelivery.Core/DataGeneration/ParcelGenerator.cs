using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.DataGeneration;

/// <summary>
/// Builds the pool of parcels waiting in the warehouse for one planning run.
/// </summary>
/// <remarks>
/// One parcel is one catalogue article. The planner sees only totals, so a basket of
/// several products would look the same to it. The same seed gives the same pool.
/// </remarks>
public static class ParcelGenerator
{
    /// <summary>
    /// Generates the parcels waiting for one planning run.
    /// </summary>
    /// <param name="mix">Which goods, and how many parcels.</param>
    /// <param name="seed">Anything generated from the same seed is identical.</param>
    public static Parcel[] Generate(DemandMix mix, ulong seed = 1)
    {
        ArgumentNullException.ThrowIfNull(mix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mix.ParcelCount);

        // Pick a category by its share, then an article within it uniformly.
        (Product[] articles, int[] categoryStart, double[] cumulativeShare) = Weight(mix);

        Pcg32 categoryRandom = new(seed, sequence: 21);
        Pcg32 articleRandom = new(seed, sequence: 22);

        Parcel[] parcels = new Parcel[mix.ParcelCount];

        for (int i = 0; i < parcels.Length; i++)
        {
            int category = PickCategory(cumulativeShare, categoryRandom.NextDouble());

            int first = categoryStart[category];
            int last = categoryStart[category + 1];
            int article = first + (int)(articleRandom.NextDouble() * (last - first));

            // In case rounding lands past the end of the category.
            article = Math.Min(article, last - 1);

            Product product = articles[article];

            parcels[i] = new Parcel(
                Id: i,
                WeightGrams: product.WeightGrams,
                VolumeCm3: product.VolumeCm3,
                ProfitHellers: product.ProfitHellers);
        }

        return parcels;
    }

    /// <summary>
    /// The mix's articles, where each category starts, and cumulative category shares.
    /// </summary>
    private static (Product[] Articles, int[] CategoryStart, double[] CumulativeShare) Weight(
        DemandMix mix)
    {
        List<Product> articles = [];
        List<int> starts = [0];
        List<double> shares = [];

        foreach ((string category, double share) in mix.Weights.OrderBy(entry => entry.Key))
        {
            if (share <= 0.0)
            {
                continue;
            }

            Product[] inCategory = ProductCatalogue.InCategory(category);

            if (inCategory.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Demand mix '{mix.Name}' names category '{category}', "
                    + "which is not in the catalogue.");
            }

            articles.AddRange(inCategory);
            starts.Add(articles.Count);
            shares.Add(share);
        }

        if (shares.Count == 0)
        {
            throw new InvalidOperationException($"Demand mix '{mix.Name}' has no categories.");
        }

        double total = shares.Sum();
        double running = 0.0;
        double[] cumulative = new double[shares.Count];

        for (int i = 0; i < shares.Count; i++)
        {
            running += shares[i] / total;
            cumulative[i] = running;
        }

        // Rounding could leave the last boundary just below one.
        cumulative[^1] = 1.0;

        return ([.. articles], [.. starts], cumulative);
    }

    /// <summary>
    /// Maps a uniform draw onto a category by its share.
    /// </summary>
    private static int PickCategory(double[] cumulativeShare, double draw)
    {
        for (int i = 0; i < cumulativeShare.Length; i++)
        {
            if (draw < cumulativeShare[i])
            {
                return i;
            }
        }

        return cumulativeShare.Length - 1;
    }
}
