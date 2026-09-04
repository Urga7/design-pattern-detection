using DesignPatternDetection.Cli;

namespace DesignPatternDetection;

internal static class Program
{
    private static Task<int> Main(string[] args) => DetectionCli.RunAsync(args);
}
