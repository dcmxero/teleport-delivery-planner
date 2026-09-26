using Spectre.Console;
using System.Globalization;
using TeleportDelivery.Core.DataGeneration;

namespace TeleportDelivery.Cli;

/// <summary>
/// Reads the CSV reports BenchmarkDotNet writes and prints them as one table.
/// </summary>
internal static class BenchmarkReport
{
    /// <summary>
    /// The order the benchmark classes are shown in.
    /// </summary>
    private static readonly string[] TypeOrder =
        ["PlannerBenchmarks", "ScalingBenchmarks", "PlanStageBenchmarks"];

    /// <summary>
    /// One measured case.
    /// </summary>
    /// <param name="Type">The benchmark class.</param>
    /// <param name="Method">The benchmark method.</param>
    /// <param name="Parameter">The demand mix or parcel count, when the class has one.</param>
    /// <param name="Mean">Mean time as BenchmarkDotNet writes it, such as "46.64 ms".</param>
    /// <param name="Allocated">Allocated memory as BenchmarkDotNet writes it.</param>
    public readonly record struct Case(
        string Type,
        string Method,
        string? Parameter,
        string Mean,
        string Allocated);

    /// <summary>
    /// Reads the given <c>*-report.csv</c> files.
    /// </summary>
    public static IReadOnlyList<Case> Read(IEnumerable<string> files)
    {
        List<Case> cases = [];

        foreach (string file in files)
        {
            // TeleportDelivery.Benchmarks.PlannerBenchmarks-report.csv -> PlannerBenchmarks
            string name = Path.GetFileNameWithoutExtension(file);
            int end = name.LastIndexOf("-report", StringComparison.Ordinal);
            string type = name[(name.LastIndexOf('.') + 1)..end];

            string[] lines = File.ReadAllLines(file);

            if (lines.Length < 2)
            {
                continue;
            }

            string[] header = Fields(lines[0]);
            int method = Array.IndexOf(header, "Method");
            int mean = Array.IndexOf(header, "Mean");
            int allocated = Array.IndexOf(header, "Allocated");
            int parameter = Array.FindIndex(header, column => column is "Mix" or "ParcelCount");

            if (method < 0 || mean < 0)
            {
                continue;
            }

            foreach (string line in lines.Skip(1).Where(line => line.Length > 0))
            {
                string[] fields = Fields(line);

                cases.Add(new Case(
                    type,
                    fields[method],
                    parameter >= 0 ? fields[parameter] : null,
                    fields[mean],
                    allocated >= 0 ? fields[allocated] : "-"));
            }
        }

        return cases;
    }

    /// <summary>
    /// What a case measures, in words: class, parameter and method.
    /// </summary>
    public static string Describe(string type, string method, string? parameter) =>
        string.Join(
            ", ",
            new[]
            {
                Texts.Benchmarks.Group(type),
                ParameterLabel(parameter),
                Texts.Benchmarks.Case(type, method),
            }
                .Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>
    /// Prints the cases as one table, grouped by benchmark class, with a legend.
    /// </summary>
    public static void Print(IReadOnlyList<Case> cases)
    {
        Table table = Tables.Create(
            Texts.Benchmarks.MeasurementColumn,
            Texts.Benchmarks.VariantColumn,
            Texts.Benchmarks.TimeColumn,
            Texts.Benchmarks.MemoryColumn);

        // One row per benchmark class, so the row separators fall between classes.
        foreach (IGrouping<string, Case> group in cases
                     .OrderBy(measured => Rank(measured.Type))
                     .ThenBy(measured => ParameterRank(measured.Parameter))
                     .GroupBy(measured => measured.Type))
        {
            List<string> labels = [$"[bold]{Texts.Benchmarks.Group(group.Key).EscapeMarkup()}[/]"];
            List<string> variants = [""];
            List<string> times = [""];
            List<string> memory = [""];
            string? parameter = null;

            foreach (Case measured in group)
            {
                string label = measured.Parameter != parameter
                    ? ParameterLabel(measured.Parameter)
                    : "";
                parameter = measured.Parameter;

                labels.Add($"  {label.EscapeMarkup()}");
                variants.Add(Texts.Benchmarks.Case(measured.Type, measured.Method).EscapeMarkup());
                string time = Measured(measured.Mean)
                    ? Time(measured.Mean)
                    : Texts.Benchmarks.NotMeasured;

                times.Add(time.PadLeft(9));
                memory.Add(Memory(measured.Allocated).PadLeft(8));
            }

            table.AddRow(
                string.Join('\n', labels),
                string.Join('\n', variants),
                string.Join('\n', times),
                string.Join('\n', memory));
        }

        Tables.Print(table);
        AnsiConsole.WriteLine();
        Tables.Legend(table, Texts.Benchmarks.Legend);
    }

    /// <summary>
    /// Whether BenchmarkDotNet has a mean at all; it writes "NA" for a failed case.
    /// </summary>
    private static bool Measured(string mean) => TrySplit(mean, out _, out _);

    private static int Rank(string type) =>
        Array.IndexOf(TypeOrder, type) is var index and >= 0 ? index : TypeOrder.Length;

    /// <summary>
    /// Days in the order the planner reports them, parcel counts by size.
    /// </summary>
    private static long ParameterRank(string? parameter)
    {
        if (parameter is null)
        {
            return 0;
        }

        if (long.TryParse(parameter, CultureInfo.InvariantCulture, out long count))
        {
            return count;
        }

        for (int i = 0; i < DemandMix.All.Count; i++)
        {
            if (DemandMix.All[i].Name == parameter)
            {
                return i;
            }
        }

        return DemandMix.All.Count;
    }

    private static string ParameterLabel(string? parameter) => parameter switch
    {
        null => "",
        // The group already says it is a parcel count.
        _ when int.TryParse(parameter, CultureInfo.InvariantCulture, out int count) =>
            count.ToString("N0", Texts.Culture),
        _ => Texts.Days.Label(parameter),
    };

    /// <summary>
    /// A mean time in milliseconds, in Slovak number format.
    /// </summary>
    public static string Time(string mean)
    {
        if (!TrySplit(mean, out double value, out string unit))
        {
            return mean;
        }

        double milliseconds = unit switch
        {
            "ns" => value / 1_000_000,
            "μs" or "us" => value / 1_000,
            "ms" => value,
            "s" => value * 1_000,
            _ => double.NaN,
        };

        if (double.IsNaN(milliseconds))
        {
            return mean;
        }

        string format = milliseconds switch
        {
            < 10 => "F2",
            < 100 => "F1",
            _ => "F0",
        };

        return $"{milliseconds.ToString(format, Texts.Culture)} {Texts.Benchmarks.Milliseconds}";
    }

    /// <summary>
    /// Allocated memory in B, KB or MB, in Slovak number format.
    /// </summary>
    /// <remarks>
    /// BenchmarkDotNet writes some reports in bytes and others in MB; 1 KB is 1024 B.
    /// </remarks>
    private static string Memory(string allocated)
    {
        if (!TrySplit(allocated, out double value, out string unit))
        {
            return allocated;
        }

        double bytes = unit switch
        {
            "B" => value,
            "KB" => value * 1024,
            "MB" => value * 1024 * 1024,
            "GB" => value * 1024 * 1024 * 1024,
            _ => double.NaN,
        };

        return bytes switch
        {
            double.NaN => allocated,
            < 1024 => $"{bytes.ToString("0", Texts.Culture)} B",
            < 1024 * 1024 => $"{(bytes / 1024).ToString("0.#", Texts.Culture)} KB",
            _ => $"{(bytes / (1024 * 1024)).ToString("0.#", Texts.Culture)} MB",
        };
    }

    private static bool TrySplit(string text, out double value, out string unit)
    {
        string[] parts = text.Split(' ', 2);
        unit = parts.Length == 2 ? parts[1] : "";

        return double.TryParse(
            parts[0],
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value) && unit.Length > 0;
    }

    /// <summary>
    /// Splits a CSV line, honouring double quotes.
    /// </summary>
    private static string[] Fields(string line)
    {
        List<string> fields = [];
        System.Text.StringBuilder field = new();
        bool quoted = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        fields.Add(field.ToString());
        return [.. fields];
    }
}
