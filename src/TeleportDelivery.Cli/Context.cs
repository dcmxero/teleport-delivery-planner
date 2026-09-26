using Spectre.Console;
using TeleportDelivery.Core.Domain;
using TeleportDelivery.Core.Planning;

namespace TeleportDelivery.Cli;

/// <summary>
/// The fleet and loading rule the reports use, and the plan table's layout.
/// </summary>
internal static class Context
{
    /// <summary>
    /// The fleet described by the assignment.
    /// </summary>
    public static FleetConfiguration Fleet { get; } = FleetConfiguration.Default;

    /// <summary>
    /// The loading rule used throughout.
    /// </summary>
    public static IVanSelection Packer { get; } = new BalancedVanSelection();

    /// <summary>
    /// Waiting volume as a multiple of the fleet's volume.
    /// </summary>
    public static double VolumePressure(IReadOnlyList<Parcel> parcels) =>
        parcels.Sum(parcel => (double)parcel.VolumeCm3) / Fleet.TotalVolumeCapacityCm3;

    /// <summary>
    /// Waiting weight as a multiple of the fleet's payload.
    /// </summary>
    public static double WeightPressure(IReadOnlyList<Parcel> parcels) =>
        parcels.Sum(parcel => (double)parcel.WeightGrams) / Fleet.TotalWeightCapacityGrams;

    /// <summary>
    /// Names the seed above a report, unless it is the default.
    /// </summary>
    public static void NoteSeed(ulong seed)
    {
        if (seed == Program.DefaultSeed)
        {
            return;
        }

        AnsiConsole.MarkupLine($"[grey]{Texts.Report.Seed(seed)}[/]");
    }

    private static readonly ColumnGroup Waiting =
        new(Texts.Plan.Waiting, 6, Texts.Plan.Weight, Texts.Plan.Volume);

    private static readonly ColumnGroup Parcels =
        new(Texts.Plan.Parcels, 9, Texts.Plan.Total, Texts.Plan.Carried, Texts.Plan.Left);

    private static readonly ColumnGroup FleetUsed =
        new(Texts.Plan.FleetUsed, 6, Texts.Plan.Weight, Texts.Plan.Volume);

    /// <summary>
    /// The waiting column: weight and volume as multiples of the fleet.
    /// </summary>
    public static string WaitingOf(IReadOnlyList<Parcel> parcels) => Waiting.Of(
        WeightPressure(parcels).ToString("F2", Texts.Culture),
        VolumePressure(parcels).ToString("F2", Texts.Culture));

    /// <summary>
    /// Starts a plan table; the first column names the day or the file.
    /// </summary>
    public static Table PlanTable(string firstColumn) => Tables.Create(
        firstColumn,
        Waiting.Header,
        Texts.Plan.Planner,
        Parcels.Header,
        Texts.Plan.Profit,
        Texts.Plan.OfBound,
        FleetUsed.Header,
        Texts.Plan.Theta,
        Texts.Plan.Milliseconds);

    /// <summary>
    /// Prints the legend under a plan table.
    /// </summary>
    public static void ExplainPlanColumns(Table table)
    {
        AnsiConsole.WriteLine();

        Tables.Legend(table, Texts.Plan.Legend);
    }

    /// <summary>
    /// Adds one pool to the table: one row, with a line per planner in each cell.
    /// </summary>
    /// <remarks>
    /// One row per day, so the row separators fall between days, not between planners.
    /// </remarks>
    public static void Row(
        Table table,
        string label,
        string waiting,
        IReadOnlyList<string[]> planners) =>
        table.AddRow(
        [
            label,
            waiting,
            .. Enumerable.Range(0, planners[0].Length)
                .Select(column => string.Join('\n', planners.Select(cells => cells[column]))),
        ]);

    /// <summary>
    /// One planner's result on one pool, from the planner column on.
    /// </summary>
    public static string[] Cells(
        string planner,
        DeliveryPlan plan,
        double bound,
        long elapsedMs) =>
        [
            planner,
            Parcels.Of(
                Count(plan.DispatchedParcelCount + plan.RejectedParcelIds.Count),
                Count(plan.DispatchedParcelCount),
                Count(plan.RejectedParcelIds.Count)),
            (plan.TotalProfitHellers / 100_000_000.0).ToString("F2", Texts.Culture)
                .PadLeft(6),
            Tables.Percent(plan.TotalProfitHellers / bound, decimals: 2).PadLeft(7),
            FleetUsed.Of(
                Tables.Percent(plan.WeightUtilisation),
                Tables.Percent(plan.VolumeUtilisation)),
            Theta(plan.Trace).PadLeft(Texts.Plan.Theta.Length),
            elapsedMs.ToString("N0", Texts.Culture).PadLeft(4),
        ];

    private static string Count(int value) =>
        value.ToString("N0", Texts.Culture);

    /// <summary>
    /// The chosen theta, or a note that everything fitted.
    /// </summary>
    public static string Theta(PlanningTrace? trace) => trace switch
    {
        { UsedFastPath: true } => Texts.Plan.Fast,
        { ChosenTheta: { } theta } => theta.ToString("F2", Texts.Culture),
        _ => "-",
    };
}
