using Spectre.Console;

namespace TeleportDelivery.Cli;

/// <summary>
/// Shows a progress bar while a report is being computed.
/// </summary>
/// <remarks>
/// Weighted by each item's size, since the days differ tenfold. Not drawn when output is
/// redirected.
/// </remarks>
internal static class Work
{
    /// <summary>
    /// Works through a list, naming each item and each stage within it as it starts.
    /// </summary>
    /// <param name="items">What to work through.</param>
    /// <param name="name">What to call an item while it is being worked on.</param>
    /// <param name="weight">How much of the bar an item takes, relative to the others.</param>
    /// <param name="stages">What each item passes through, in order.</param>
    /// <param name="step">The work, given a callback to call as each stage finishes.</param>
    public static void Steps<T>(
        IReadOnlyList<T> items,
        Func<T, string> name,
        Func<T, double> weight,
        IReadOnlyList<string> stages,
        Action<T, Action> step)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentOutOfRangeException.ThrowIfZero(stages.Count);

        if (Console.IsOutputRedirected || items.Count == 0)
        {
            foreach (T item in items)
            {
                step(item, () => { });
            }

            return;
        }

        double total = items.Sum(weight);
        double done = 0;
        string description = Describe(name(items[0]), stages[0]);
        bool finished = false;
        int frame = 0;
        Lock gate = new();

        // Drawn by hand: a progress column row cannot hold the text below the bar, and
        // text beside it would shift the bar whenever its length changes.
        AnsiConsole.Live(Text.Empty)
            .AutoClear(true)
            .Start(context =>
            {
                void Show()
                {
                    lock (gate)
                    {
                        if (!finished)
                        {
                            context.UpdateTarget(Render(done / total, description, frame++));
                        }
                    }
                }

                // Also redrawn on a timer, so the spinner turns during a long stage.
                using Timer timer = new(
                    _ => Show(), null, TimeSpan.Zero, Spinner.Known.Dots.Interval);

                foreach (T item in items)
                {
                    double share = weight(item) / stages.Count;
                    int stage = 0;

                    lock (gate)
                    {
                        description = Describe(name(item), stages[0]);
                    }

                    step(item, () =>
                    {
                        lock (gate)
                        {
                            done += share;
                            stage++;

                            if (stage < stages.Count)
                            {
                                description = Describe(name(item), stages[stage]);
                            }
                        }

                        Show();
                    });

                    // Stages the work did not report still count.
                    lock (gate)
                    {
                        done = Math.Min(done + (share * (stages.Count - stage)), total);
                    }
                }

                lock (gate)
                {
                    finished = true;
                }
            });
    }

    private const int BarWidth = 40;

    /// <summary>
    /// The spinner, bar and percentage on one line, what is being done below it.
    /// </summary>
    private static Rows Render(double fraction, string description, int frame)
    {
        int filled = (int)Math.Round(Math.Clamp(fraction, 0, 1) * BarWidth);
        IReadOnlyList<string> frames = Spinner.Known.Dots.Frames;

        return new Rows(
            new Markup(
                $"{frames[frame % frames.Count]} "
                + $"[teal]{new string('━', filled)}[/]"
                + $"[grey]{new string('━', BarWidth - filled)}[/] "
                + Tables.Percent(fraction, decimals: 0)),
            new Markup($"  {description}"));
    }

    private static string Describe(string item, string stage) => $"{item} [grey]{stage}[/]";
}
