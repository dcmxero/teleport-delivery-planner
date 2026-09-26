using Spectre.Console;
using Spectre.Console.Rendering;
using System.Globalization;

namespace TeleportDelivery.Cli;

/// <summary>
/// Shared table layout: ruled tables, two-line headings, legends.
/// </summary>
internal static class Tables
{
    /// <summary>
    /// A table ruled in dim grey.
    /// </summary>
    /// <param name="headers">
    /// One per column; a newline splits a heading into the group and its figures.
    /// </param>
    public static Table Create(params string[] headers)
    {
        Table table = new Table()
            .Border(TableBorder.Square)
            .BorderColor(Color.Grey35)
            .ShowRowSeparators();

        foreach (string header in headers)
        {
            table.AddColumn(new TableColumn(new Markup(Heading(header))).NoWrap());
        }

        return table;
    }

    /// <summary>
    /// Renders a heading, dimming the second line so the group reads first.
    /// </summary>
    private static string Heading(string header)
    {
        string[] lines = header.Split('\n');

        return lines.Length == 1
            ? $"[bold]{lines[0]}[/]"
            : $"[bold]{lines[0]}[/]\n[grey]{lines[1]}[/]";
    }

    /// <summary>
    /// A share as a percentage, without the space before the sign.
    /// </summary>
    public static string Percent(double share, int decimals = 1) =>
        share.ToString("P" + decimals.ToString(CultureInfo.InvariantCulture), Texts.Culture)
            .Replace(" %", "%", StringComparison.Ordinal);

    /// <summary>
    /// Prints the notes under a table, each wrapped to the table's width.
    /// </summary>
    public static void Legend(Table table, params (string Term, string Text)[] entries)
    {
        ArgumentNullException.ThrowIfNull(table);

        int width = ((IRenderable)table)
            .Measure(RenderOptions.Create(AnsiConsole.Console), AnsiConsole.Profile.Width).Max;

        Grid grid = new() { Width = width - Indent };
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn(new GridColumn());

        foreach ((string term, string text) in entries)
        {
            grid.AddRow(new Markup($"[bold]{term}[/]"), new Markup(text));
        }

        AnsiConsole.Write(new Padder(grid, new Padding(Indent, 0, 0, 0)));
    }

    private const int Indent = 2;

    /// <summary>
    /// Prints a table after a blank line.
    /// </summary>
    public static void Print(Table table)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
    }
}
