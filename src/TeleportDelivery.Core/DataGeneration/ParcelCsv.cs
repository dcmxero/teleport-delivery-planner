using System.Globalization;
using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.DataGeneration;

/// <summary>
/// Reads and writes parcel pools as CSV.
/// </summary>
/// <remarks>
/// In the planner's integer units, so a round trip changes nothing.
/// </remarks>
public static class ParcelCsv
{
    private const string Header = "id,weight_grams,volume_cm3,profit_hellers";

    /// <summary>
    /// Writes a parcel pool, one parcel per line.
    /// </summary>
    public static void Write(string path, IReadOnlyList<Parcel> parcels)
    {
        ArgumentNullException.ThrowIfNull(parcels);

        using StreamWriter writer = new(path);
        writer.Write(Header);
        writer.Write('\n');

        foreach (Parcel parcel in parcels)
        {
            writer.Write(parcel.Id);
            writer.Write(',');
            writer.Write(parcel.WeightGrams);
            writer.Write(',');
            writer.Write(parcel.VolumeCm3);
            writer.Write(',');
            writer.Write(parcel.ProfitHellers);
            writer.Write('\n');
        }
    }

    /// <summary>
    /// Reads a parcel pool, skipping the header and blank lines.
    /// </summary>
    /// <exception cref="FormatException">
    /// A row is malformed, or its weight, volume or profit is not positive.
    /// </exception>
    public static IReadOnlyList<Parcel> Read(string path)
    {
        List<Parcel> parcels = [];

        // Outside the loop: a stackalloc inside it would grow the stack per line.
        Span<Range> fields = stackalloc Range[4];

        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0 || line.StartsWith("id,", StringComparison.Ordinal))
            {
                continue;
            }

            ReadOnlySpan<char> row = line;

            if (row.Split(fields, ',') != 4)
            {
                throw new FormatException($"Expected four fields in '{line}'.");
            }

            Parcel parcel = new(
                Id: int.Parse(row[fields[0]], CultureInfo.InvariantCulture),
                WeightGrams: int.Parse(row[fields[1]], CultureInfo.InvariantCulture),
                VolumeCm3: int.Parse(row[fields[2]], CultureInfo.InvariantCulture),
                ProfitHellers: long.Parse(row[fields[3]], CultureInfo.InvariantCulture));

            // A zero or negative figure would rank the parcel ahead of everything else.
            if (parcel.WeightGrams <= 0 || parcel.VolumeCm3 <= 0 || parcel.ProfitHellers <= 0)
            {
                throw new FormatException(
                    $"Weight, volume and profit must be positive in '{line}'.");
            }

            parcels.Add(parcel);
        }

        return parcels;
    }
}
