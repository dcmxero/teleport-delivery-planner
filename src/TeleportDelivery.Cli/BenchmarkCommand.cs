using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TeleportDelivery.Cli;

/// <summary>
/// Runs the benchmark project in Release as a separate process.
/// </summary>
/// <remarks>
/// Its raw output stays off the screen: one line per finished measurement, a summary
/// table at the end. BenchmarkDotNet keeps the full output in its own log.
/// </remarks>
internal static partial class BenchmarkCommand
{
    private const int TailLines = 15;

    private static readonly string ProjectPath =
        Path.Combine(
            "benchmarks", "TeleportDelivery.Benchmarks", "TeleportDelivery.Benchmarks.csproj");

    /// <summary>
    /// Runs all benchmarks and prints a summary of the results.
    /// </summary>
    public static void Run()
    {
        AnsiConsole.MarkupLine(Texts.Benchmarks.Explanation);
        AnsiConsole.MarkupLine($"[grey]{Texts.Benchmarks.Warning}[/]");
        AnsiConsole.WriteLine();

        string? root = FindRepositoryRoot();

        if (root is null)
        {
            string notFound = Texts.Benchmarks.ProjectNotFound(ProjectPath).EscapeMarkup();
            AnsiConsole.MarkupLine($"[red]{notFound}[/]");
            return;
        }

        if (!SdkIsAvailable())
        {
            AnsiConsole.MarkupLine($"[red]{Texts.Benchmarks.SdkMissing}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[grey]{Texts.Benchmarks.CommandLine.EscapeMarkup()}[/]");

        DateTime started = DateTime.UtcNow;
        (int exitCode, int total, int measured, string[] tail) = Execute(root);

        string artifacts = Path.Combine(root, "BenchmarkDotNet.Artifacts");
        string reports = Path.Combine(artifacts, "results");

        string[] written = Directory.Exists(reports)
            ? [.. Directory.EnumerateFiles(reports, "*-report.csv")
                .Where(file => File.GetLastWriteTimeUtc(file) >= started)]
            : [];

        // Judged by what was measured: BenchmarkDotNet exits with 0 and writes a report
        // even when a build failure left every benchmark unmeasured.
        if (exitCode != 0 || measured == 0 || written.Length == 0)
        {
            AnsiConsole.WriteLine();
            string failure = exitCode != 0
                ? Texts.Benchmarks.Failed(exitCode)
                : Texts.Benchmarks.NothingMeasured;
            AnsiConsole.MarkupLine($"[red]{failure}[/]");
            ShowTail(tail, artifacts, started);
            return;
        }

        BenchmarkReport.Print(BenchmarkReport.Read(written));
        AnsiConsole.WriteLine();

        if (measured < total)
        {
            AnsiConsole.MarkupLine($"[red]{Texts.Benchmarks.PartlyMeasured(measured, total)}[/]");
            ShowTail([], artifacts, started);
        }

        AnsiConsole.MarkupLine(Texts.Benchmarks.Reports(reports).EscapeMarkup());
    }

    /// <summary>
    /// Runs the benchmarks, printing one line per finished measurement.
    /// </summary>
    /// <returns>
    /// The exit code, how many benchmarks there were and were measured, and the last lines.
    /// </returns>
    private static (int ExitCode, int Total, int Measured, string[] Tail) Execute(string root)
    {
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        string[] arguments =
            ["run", "-c", "Release", "--project", ProjectPath, "--", "--filter", "*"];

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Filled from the two output streams' threads, printed from this one.
        Queue<string> tail = new();
        Queue<string> finished = new();
        int total = 0;
        int current = 0;
        string running = string.Empty;
        bool reported = false;
        int measured = 0;

        void Read(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (tail)
            {
                tail.Enqueue(line);

                if (tail.Count > TailLines)
                {
                    tail.Dequeue();
                }

                if (TotalLine().Match(line) is { Success: true } found)
                {
                    total = int.Parse(found.Groups[1].Value, CultureInfo.InvariantCulture);
                }
                else if (BenchmarkLine().Match(line) is { Success: true } benchmark)
                {
                    current++;
                    reported = false;
                    running = BenchmarkReport.Describe(
                        benchmark.Groups[1].Value,
                        benchmark.Groups[2].Value,
                        benchmark.Groups[3].Success ? benchmark.Groups[3].Value : null);
                }
                // Only the first: each group's summary repeats every mean.
                else if (MeanLine().Match(line) is { Success: true } mean
                    && current > 0 && !reported)
                {
                    reported = true;
                    measured++;
                    finished.Enqueue(
                        $"  {Texts.Benchmarks.Step(current, total),6}  {running,-58} "
                        + $"{BenchmarkReport.Time(mean.Groups[1].Value),9}");
                }
            }
        }

        int exitCode = -1;
        Stopwatch elapsed = Stopwatch.StartNew();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start(Texts.Benchmarks.Building, context =>
            {
                using Process? process = Process.Start(start);

                if (process is null)
                {
                    return;
                }

                process.OutputDataReceived += (_, e) => Read(e.Data);
                process.ErrorDataReceived += (_, e) => Read(e.Data);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exited;

                do
                {
                    exited = process.WaitForExit(250);

                    // After exit, the parameterless wait also drains the output streams.
                    if (exited)
                    {
                        process.WaitForExit();
                    }

                    lock (tail)
                    {
                        while (finished.TryDequeue(out string? line))
                        {
                            AnsiConsole.MarkupLine(line.EscapeMarkup());
                        }

                        string time = elapsed.Elapsed
                            .ToString(@"m\:ss", CultureInfo.InvariantCulture);
                        context.Status(current == 0
                            ? $"{Texts.Benchmarks.Building}  [grey]{time}[/]"
                            : $"{Texts.Benchmarks.Step(current, total)}  "
                                + $"{running.EscapeMarkup()}  [grey]{time}[/]");
                    }
                }
                while (!exited);

                exitCode = process.ExitCode;
            });

        lock (tail)
        {
            return (exitCode, total, measured, [.. tail]);
        }
    }

    /// <summary>
    /// Prints the last lines of the output and where BenchmarkDotNet kept the rest.
    /// </summary>
    private static void ShowTail(string[] tail, string artifacts, DateTime started)
    {
        if (tail.Length > 0)
        {
            AnsiConsole.MarkupLine($"[grey]{Texts.Benchmarks.LastLines}[/]");

            foreach (string line in tail)
            {
                AnsiConsole.MarkupLine($"[grey]  {line.EscapeMarkup()}[/]");
            }
        }

        string? log = Directory.Exists(artifacts)
            ? Directory.EnumerateFiles(artifacts, "*.log")
                .Where(file => File.GetLastWriteTimeUtc(file) >= started)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        if (log is not null)
        {
            AnsiConsole.MarkupLine(Texts.Benchmarks.FullLog(log).EscapeMarkup());
        }
    }

    /// <summary>
    /// Finds the repository root above the runner's own folder.
    /// </summary>
    /// <remarks>
    /// Not the working directory, which differs between Visual Studio and a terminal.
    /// </remarks>
    private static string? FindRepositoryRoot()
    {
        string location = Path.GetDirectoryName(typeof(BenchmarkCommand).Assembly.Location)
            ?? AppContext.BaseDirectory;

        for (DirectoryInfo? directory = new(location);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, ProjectPath)))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a .NET SDK is installed; a runtime alone cannot build the project.
    /// </summary>
    private static bool SdkIsAvailable()
    {
        ProcessStartInfo start = new("dotnet", "--list-sdks")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using Process? process = Process.Start(start);

            if (process is null)
            {
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && output.Trim().Length > 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// "// ***** Found 17 benchmark(s) in total *****"
    /// </summary>
    [GeneratedRegex(@"^// \*+ Found (\d+) benchmark\(s\) in total")]
    private static partial Regex TotalLine();

    /// <summary>
    /// "// Benchmark: PlannerBenchmarks.Baseline: ShortRun(...) [Mix=OrdinaryDay]"
    /// </summary>
    [GeneratedRegex(@"^// Benchmark: (\w+)\.(\w+): .*?(?:\[\w+=(\w+)\])?$")]
    private static partial Regex BenchmarkLine();

    /// <summary>
    /// "Mean = 46.411 ms, StdErr = 0.122 ms (0.26%), N = 3, StdDev = 0.212 ms"
    /// </summary>
    [GeneratedRegex(@"^Mean = ([^,]+),")]
    private static partial Regex MeanLine();
}
