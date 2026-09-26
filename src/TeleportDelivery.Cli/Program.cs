using Spectre.Console;
using System.Text;
using System.Globalization;
using TeleportDelivery.Core.DataGeneration;

namespace TeleportDelivery.Cli;

/// <summary>
/// Command line entry point: runs a command, or offers the menu.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The narrowest screen the report tables were laid out for.
    /// </summary>
    private const int MinimumWidth = 146;

    private static int Main(string[] args)
    {
        // UTF-8, or the legacy code page drops Slovak carons; no BOM in redirected output.
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // Otherwise a narrow terminal squeezes every column instead of wrapping the row.
        AnsiConsole.Profile.Width = Math.Max(AnsiConsole.Profile.Width, MinimumWidth);

        if (args.Length > 0)
        {
            return Run(args);
        }

        // No menu without an interactive console.
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            PrintUsage();
            return 1;
        }

        return Browse();
    }

    private static int Run(string[] args)
    {
        try
        {
            return args[0] switch
            {
                Texts.Commands.Plan => PlanCommand.Run(args),
                _ => Unknown(args[0]),
            };
        }
        catch (ArgumentException error)
        {
            // Thrown with a Slovak message by the argument parsing below.
            Console.Error.WriteLine(error.Message);
            PrintUsage();
            return 1;
        }
    }

    /// <summary>
    /// The interactive menu; each report shows the command line that repeats it.
    /// </summary>
    private static int Browse()
    {
        MenuEntry[] commands =
        [
            new(Texts.Menu.PlanLabel, Texts.Commands.Plan, Texts.Menu.PlanNote),
            new(Texts.Menu.BenchmarksLabel, Texts.Commands.Benchmarks, Texts.Menu.BenchmarksNote),
            new(Texts.Menu.QuitLabel, Texts.Commands.Quit, string.Empty),
        ];

        int width = commands.Max(entry => entry.Label.Length);

        AnsiConsole.Write(new FigletText(Texts.Menu.Banner).Color(Color.Teal));

        foreach (string line in Texts.Menu.Introduction)
        {
            AnsiConsole.MarkupLine(line);
        }

        while (true)
        {
            AnsiConsole.WriteLine();

            string chosen = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold]{Texts.Menu.Question}[/]")
                    .HighlightStyle(new Style(Color.Teal))
                    .AddChoices(commands.Select(entry => entry.Command))
                    .UseConverter(command => Describe(command, commands, width)));

            if (chosen == Texts.Commands.Quit)
            {
                return 0;
            }

            if (chosen == Texts.Commands.Benchmarks)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule($"[teal]{Texts.Benchmarks.Heading}[/]").LeftJustified());
                BenchmarkCommand.Run();
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[grey]{Texts.Menu.PressToReturn}[/]");
                Console.ReadKey(intercept: true);
                continue;
            }

            // Back to the day list, not the top; the list has its own way back.
            while (true)
            {
                string[] args = WithMix(chosen);

                if (args.Length == 0)
                {
                    break;
                }

                Show(args, Texts.Menu.PressForAnotherDay);
            }
        }
    }

    /// <summary>
    /// Runs one report under a heading, and waits so that it can be read.
    /// </summary>
    private static void Show(string[] args, string prompt)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[teal]{Heading(args)}[/]").LeftJustified());
        AnsiConsole.MarkupLine(
            $"[grey]{Texts.Menu.CommandLinePrefix} {string.Join(' ', args)}[/]");
        AnsiConsole.WriteLine();

        Run(args);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().LeftJustified());
        AnsiConsole.MarkupLine($"[grey]{prompt}[/]");
        Console.ReadKey(intercept: true);
    }

    /// <summary>
    /// What a run is called on screen: the command, and the demand mix it was given.
    /// </summary>
    private static string Heading(string[] args) => args switch
    {
        [var command, Texts.Arguments.Mix, var mix] => $"{command} · {Texts.Days.Label(mix)}",
        [var command] => $"{command} · {Texts.Menu.AllDaysHeading}",
        _ => string.Join(' ', args),
    };

    /// <summary>
    /// One line of the menu.
    /// </summary>
    /// <param name="Label">What the menu calls it.</param>
    /// <param name="Command">The command it stands for.</param>
    /// <param name="Note">What choosing it shows.</param>
    private readonly record struct MenuEntry(string Label, string Command, string Note);

    /// <summary>
    /// Lays a menu line out as the name and then what choosing it shows.
    /// </summary>
    private static string Describe(string command, MenuEntry[] commands, int width)
    {
        MenuEntry entry = commands.First(item => item.Command == command);

        return entry.Note.Length == 0
            ? entry.Label
            : $"{entry.Label.PadRight(width)}   [grey]{entry.Note}[/]";
    }

    /// <summary>
    /// Asks which day to plan; returns no arguments when the reader goes back.
    /// </summary>
    private static string[] WithMix(string command)
    {
        // Choices are the ASCII names; the converter shows the Slovak labels.
        const string Everything = Texts.Arguments.AllMixes;
        const string Back = Texts.Commands.Quit;

        int width = DemandMix.All.Max(mix => Texts.Days.Label(mix.Name).Length);
        const int CountWidth = 9;

        string chosen = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]{Texts.Menu.DayQuestion}[/]\n[grey]{Texts.Menu.DayCountNote}[/]\n")
                .HighlightStyle(new Style(Color.Teal))
                .PageSize(14)
                .AddChoices([Everything, .. DemandMix.All.Select(mix => mix.Name), Back])
                .UseConverter(name => name switch
                {
                    Everything =>
                        $"{Texts.Menu.AllDaysLabel.PadRight(width)}   "
                            + $"[grey]{Texts.Menu.AllDaysNote}[/]",
                    Back =>
                        $"{Texts.Menu.BackLabel.PadRight(width)}   [grey]{Texts.Menu.BackNote}[/]",
                    _ => $"{Texts.Days.Label(name).PadRight(width)}   "
                        + $"[grey]{Waiting(name),CountWidth}[/]",
                }));

        static string Waiting(string name) =>
            DemandMix.ByName(name).ParcelCount.ToString("N0", Texts.Culture);

        return chosen switch
        {
            Back => [],
            Everything => [command],
            _ => [command, Texts.Arguments.Mix, chosen],
        };
    }

    /// <summary>
    /// Reads which demand mixes to run, optionally overriding how many parcels they hold.
    /// </summary>
    internal static IReadOnlyList<DemandMix> Mixes(string[] args)
    {
        string which = Argument(args, Texts.Arguments.Mix) ?? Texts.Arguments.AllMixes;

        IReadOnlyList<DemandMix> mixes = which.Equals(
                Texts.Arguments.AllMixes, StringComparison.OrdinalIgnoreCase)
            ? DemandMix.All
            : [DemandMix.All.FirstOrDefault(
                    mix => mix.Name.Equals(which, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(Texts.Usage.UnknownDay(which))];

        if (Argument(args, Texts.Arguments.Count) is not { } count)
        {
            return mixes;
        }

        int parcels = (int)WholeNumber(Texts.Arguments.Count, count, minimum: 1);
        return [.. mixes.Select(mix => mix.WithParcelCount(parcels))];
    }

    /// <summary>
    /// The seed every reported figure in this repository was produced with.
    /// </summary>
    internal const ulong DefaultSeed = 1;

    internal static ulong Seed(string[] args) =>
        Argument(args, Texts.Arguments.Seed) is { } seed
            ? WholeNumber(Texts.Arguments.Seed, seed, minimum: 0)
            : DefaultSeed;

    /// <summary>
    /// A whole number given to a flag, or an argument error saying which flag.
    /// </summary>
    private static ulong WholeNumber(string argument, string value, ulong minimum) =>
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong number)
            && number >= minimum && number <= int.MaxValue
            ? number
            : throw new ArgumentException(Texts.Usage.NotANumber(argument, value));

    /// <summary>
    /// Breaks a description into lines short enough to read in a terminal.
    /// </summary>
    private static IEnumerable<string> Wrap(string text, int width)
    {
        string line = string.Empty;

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length + 1 > width)
            {
                yield return line;
                line = word;
                continue;
            }

            line = line.Length == 0 ? word : $"{line} {word}";
        }

        if (line.Length > 0)
        {
            yield return line;
        }
    }

    /// <summary>
    /// Reads the value following a named flag, or null when the flag is absent.
    /// </summary>
    internal static string? Argument(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine(Texts.Usage.UnknownCommand(command));
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(Texts.Usage.Title);

        foreach (string line in Texts.Usage.Lines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
        Console.WriteLine(Texts.Usage.DaysTitle);
        Console.WriteLine();

        foreach (DemandMix mix in DemandMix.All)
        {
            Console.WriteLine(
                $"  {mix.Name,-18} {Texts.Days.Label(mix.Name),-20} "
                + $"{Texts.Menu.Parcels(mix.ParcelCount),19}");

            foreach (string line in Wrap(Texts.Days.Note(mix.Name), width: 70))
            {
                Console.WriteLine($"  {"",-18} {line}");
            }

            Console.WriteLine();
        }
    }
}
