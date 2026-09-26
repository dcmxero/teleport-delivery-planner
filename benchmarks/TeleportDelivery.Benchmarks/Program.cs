using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace TeleportDelivery.Benchmarks;

/// <summary>
/// Runs the benchmarks.
/// </summary>
/// <remarks>
/// ShortRun (three warm-up and three measured iterations) to keep the run to minutes;
/// pass --job default for the full one.
/// </remarks>
internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args, DefaultConfig.Instance.AddJob(Job.ShortRun));
}
