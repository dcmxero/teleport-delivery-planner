using Spectre.Console;
using System.Diagnostics;
using TeleportDelivery.Core.DataGeneration;
using TeleportDelivery.Core.Domain;
using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Cli;

/// <summary>
/// Runs both planners over a pool, generated or read from a file.
/// </summary>
internal static class PlanCommand
{
    /// <summary>
    /// Runs both planners over one demand mix, or all of them.
    /// </summary>
    public static int Run(string[] args)
    {
        if (Program.Argument(args, Texts.Arguments.Input) is { } inputPath)
        {
            return PlanFile(inputPath);
        }

        IReadOnlyList<DemandMix> mixes = Program.Mixes(args);
        ulong seed = Program.Seed(args);

        Context.NoteSeed(seed);

        Table table = Context.PlanTable(Texts.Plan.DayColumn);

        string? csvPath = Program.Argument(args, Texts.Arguments.Csv);

        Work.Steps(
            mixes,
            mix => Texts.Days.Label(mix.Name),
            mix => mix.ParcelCount,
            [
                Texts.Report.StageGenerating,
                Texts.Report.StageBound,
                Texts.Plan.Baseline,
                Texts.Plan.Adaptive,
            ],
            (mix, done) =>
            {
                Parcel[] parcels = ParcelGenerator.Generate(mix, seed);
                done();

                Compare(table, Texts.Days.Label(mix.Name), parcels, done);

                if (csvPath is not null)
                {
                    Export(csvPath, mix.Name, parcels, mixes.Count);
                }
            });

        Tables.Print(table);
        Context.ExplainPlanColumns(table);

        return 0;
    }

    /// <summary>
    /// Plans a pool read from a file, such as a real export.
    /// </summary>
    private static int PlanFile(string path)
    {
        IReadOnlyList<Parcel> parcels;

        try
        {
            parcels = ParcelCsv.Read(path);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            Console.Error.WriteLine(Texts.Report.NotFound(path));
            return 1;
        }
        catch (Exception error) when (error is IOException or FormatException or OverflowException)
        {
            // The reason names the offending row.
            Console.Error.WriteLine(Texts.Report.Unreadable(path, error.Message));
            return 1;
        }

        if (parcels.Count == 0)
        {
            Console.Error.WriteLine(Texts.Report.NoParcelsRead(path));
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]{Texts.Report.Source(path).EscapeMarkup()}[/]");

        Table table = Context.PlanTable(Texts.Plan.SourceColumn);
        Compare(table, Path.GetFileName(path), [.. parcels], () => { });

        Tables.Print(table);
        Context.ExplainPlanColumns(table);

        return 0;
    }

    /// <summary>
    /// Runs both planners over the same parcels, one row each.
    /// </summary>
    private static void Compare(
        Table table,
        string label,
        Parcel[] parcels,
        Action stageDone)
    {
        double bound = ProfitUpperBound.Compute(parcels, Context.Fleet);
        stageDone();

        string waiting = Context.WaitingOf(parcels);

        (string Name, IDeliveryPlanner Planner)[] planners =
        [
            (Texts.Plan.Baseline,
                new GreedyPlanner(ResourcePrice.Balanced(Context.Fleet), Context.Packer)),
            (Texts.Plan.Adaptive, new AdaptivePlanner(Context.Packer)),
        ];

        List<string[]> results = [];

        foreach ((string name, IDeliveryPlanner planner) in planners)
        {
            Warm(planner, parcels);

            Stopwatch stopwatch = Stopwatch.StartNew();
            DeliveryPlan plan = planner.Plan(parcels, Context.Fleet);
            stopwatch.Stop();

            results.Add(Context.Cells(name, plan, bound, stopwatch.ElapsedMilliseconds));
            stageDone();
        }

        Context.Row(table, label, waiting, results);
    }

    private static readonly HashSet<string> Warmed = [];

    /// <summary>
    /// Runs a planner once, untimed, so the timed run does not include JIT compilation.
    /// </summary>
    /// <remarks>
    /// Once per planner, not per day: JIT compilation happens only once.
    /// </remarks>
    private static void Warm(IDeliveryPlanner planner, Parcel[] parcels)
    {
        if (!Warmed.Add(planner.Name))
        {
            return;
        }

        planner.Plan(parcels, Context.Fleet);
    }

    /// <summary>
    /// Writes the parcels as CSV, one file per day when there are several.
    /// </summary>
    private static void Export(string csvPath, string mixName, Parcel[] parcels, int mixCount)
    {
        string path = mixCount == 1
            ? csvPath
            : Path.Combine(
                Path.GetDirectoryName(csvPath) ?? ".",
                $"{Path.GetFileNameWithoutExtension(csvPath)}-{mixName}.csv");

        ParcelCsv.Write(path, parcels);
        // Escaped, because a bracket in a path would be read as markup.
        AnsiConsole.MarkupLine($"[grey]{Texts.Report.WrittenTo(path).EscapeMarkup()}[/]");
    }
}
