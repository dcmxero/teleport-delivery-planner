using TeleportDelivery.Core.DataGeneration;
using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Tests;

/// <summary>
/// The catalogue and the parcel pools drawn from it.
/// </summary>
public class DataGenerationTests
{
    private static readonly FleetConfiguration Fleet = FleetConfiguration.Default;

    [Fact]
    public void TheCatalogueMatchesTheCopyInTheRepository()
    {
        // data/catalogue.csv is what a reader sees; it must be what the code generates.
        string stored = RepositoryFile(Path.Combine("data", "catalogue.csv"));

        Assert.Equal(ProductCatalogue.All.ToArray(), ProductCatalogue.Read(stored));
    }

    [Fact]
    public void EveryArticleFitsAVan()
    {
        Assert.All(ProductCatalogue.All.ToArray(), article =>
        {
            Assert.InRange(article.VolumeCm3, 1, Fleet.VanVolumeCapacityCm3);
            Assert.InRange(article.WeightGrams, 1, Fleet.VanWeightCapacityGrams);
            Assert.True(article.ProfitHellers > 0);
        });
    }

    [Fact]
    public void AMixDrawsOnlyFromTheCategoriesItNames()
    {
        foreach (DemandMix mix in DemandMix.All)
        {
            Parcel[] parcels = ParcelGenerator.Generate(mix.WithParcelCount(20_000), seed: 5);

            HashSet<(int Volume, int Weight, long Profit)> allowed =
            [
                .. mix.Weights.Keys
                    .SelectMany(ProductCatalogue.InCategory)
                    .Select(article =>
                        (article.VolumeCm3, article.WeightGrams, article.ProfitHellers)),
            ];

            Assert.All(parcels, parcel => Assert.Contains(
                (parcel.VolumeCm3, parcel.WeightGrams, parcel.ProfitHellers), allowed));
        }
    }

    [Fact]
    public void TheSameSeedGivesTheSameParcels()
    {
        foreach (DemandMix mix in DemandMix.All)
        {
            DemandMix small = mix.WithParcelCount(5_000);

            Parcel[] first = ParcelGenerator.Generate(small, seed: 42);

            Assert.Equal(first, ParcelGenerator.Generate(small, seed: 42));
            Assert.NotEqual(first, ParcelGenerator.Generate(small, seed: 43));
        }
    }

    [Fact]
    public void ParcelSizeDoesNotDependOnHowManyThereAre()
    {
        // The scaling benchmark changes only the count, so the goods must stay the same.
        DemandMix mix = DemandMix.ByName("OrdinaryDay");

        Parcel[] small = ParcelGenerator.Generate(mix.WithParcelCount(50_000), seed: 5);
        Parcel[] large = ParcelGenerator.Generate(mix.WithParcelCount(500_000), seed: 5);

        double smallMedian = MedianVolume(small);
        double largeMedian = MedianVolume(large);

        Assert.InRange(largeMedian, smallMedian * 0.9, smallMedian * 1.1);
        Assert.InRange(TotalVolume(large) / TotalVolume(small), 9.0, 11.0);
    }

    [Fact]
    public void TheMixesCoverEveryRegimeThePlannerHasToHandle()
    {
        Dictionary<string, (double Volume, double Weight)> pressures = [];

        foreach (DemandMix mix in DemandMix.All)
        {
            Parcel[] parcels = ParcelGenerator.Generate(mix, seed: 1);

            pressures[mix.Name] = (
                parcels.Sum(parcel => (double)parcel.VolumeCm3) / Fleet.TotalVolumeCapacityCm3,
                parcels.Sum(parcel => (double)parcel.WeightGrams) / Fleet.TotalWeightCapacityGrams);
        }

        // Everything fits.
        Assert.True(pressures["QuietDay"].Volume < 1.0 && pressures["QuietDay"].Weight < 1.0);

        // Room runs out first.
        Assert.True(pressures["BulkyGoods"].Volume > pressures["BulkyGoods"].Weight * 3.0);

        // Payload runs out first.
        Assert.True(pressures["DenseGoods"].Weight > pressures["DenseGoods"].Volume);

        // Both bind at once.
        (double volume, double weight) = pressures["MixedExtremes"];
        Assert.InRange(weight / volume, 0.7, 1.4);
        Assert.True(volume > 1.0);
    }

    [Fact]
    public void ParcelCsvColumnsMeanWhatTheHeaderSays()
    {
        const string Csv = "id,weight_grams,volume_cm3,profit_hellers\n7,1500,2000,12345\n";
        Parcel expected = new(Id: 7, WeightGrams: 1_500, VolumeCm3: 2_000, ProfitHellers: 12_345);

        string path = Path.Combine(Path.GetTempPath(), $"parcels-{Guid.NewGuid():N}.csv");

        try
        {
            File.WriteAllText(path, Csv);
            Assert.Equal([expected], ParcelCsv.Read(path));

            ParcelCsv.Write(path, [expected]);
            Assert.Equal(Csv, File.ReadAllText(path));

            File.WriteAllText(path, "id,weight_grams,volume_cm3,profit_hellers\n8,0,2000,100\n");
            Assert.Throws<FormatException>(() => ParcelCsv.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static int MedianVolume(Parcel[] parcels) =>
        parcels.Select(parcel => parcel.VolumeCm3).Order().ElementAt(parcels.Length / 2);

    private static double TotalVolume(Parcel[] parcels) =>
        parcels.Sum(parcel => (double)parcel.VolumeCm3);

    /// <summary>
    /// A file in the repository, found by walking up from the test binaries.
    /// </summary>
    private static string RepositoryFile(string relativePath)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"{relativePath} not found above {AppContext.BaseDirectory}.");
    }
}
