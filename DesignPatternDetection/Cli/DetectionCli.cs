using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.InputResolution;
using DesignPatternDetection.Detection.Verification;
using DesignPatternDetection.Reporting;
using VDS.RDF;

namespace DesignPatternDetection.Cli;

/// <summary>
/// The detector command line, end to end: parses the arguments, resolves the inputs - cloning any repository URL
/// among them - runs either the detectors or the user's own SPARQL, writes the outputs that were asked for, and
/// deletes every clone on success, error and Ctrl+C alike.
/// </summary>
public sealed class DetectionCli
{
    private readonly RepositoryCheckouts _checkouts = new();

    /// <summary>Runs one invocation and returns the process exit code.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("Starting Design Pattern Detection...\n");

        var cli = new DetectionCli();
        Console.CancelKeyPress += (_, _) => cli._checkouts.Cleanup();

        try
        {
            await cli.ExecuteAsync(CommandLineOptions.Parse(args));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
        finally
        {
            cli._checkouts.Cleanup();
        }
    }

    private async Task ExecuteAsync(CommandLineOptions options)
    {
        var files = options.Inputs.Count > 0
            ? options.Inputs.SelectMany(SourceFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : SourceFileResolver.Resolve(ExamplesDirectory.Locate());

        if (options.QueryPath is not null)
        {
            var source = SourceGraphBuilder.Build(files);
            WriteTurtle(options.TurtlePath, source.Graph);
            SparqlQueryRunner.Run(Console.Out, source.Graph, File.ReadAllText(options.QueryPath));

            return;
        }

        var engine = new PatternDetectionEngine();
        var result = engine.Scan(files);

        if (options.Verify)
            result = await ReviewAsync(engine, result, options);

        engine.Report(result);
        WriteTurtle(options.TurtlePath, result.Source.Graph);
        ReportOutputWriter.Write(result, options);
    }

    /// <summary>The source files of one positional input; a GitHub URL is cloned into a temp directory first.</summary>
    private IEnumerable<string> SourceFiles(string argument) =>
        GitHubRepositoryUrl.TryParse(argument, out var url)
            ? SourceFileResolver.Resolve(_checkouts.Clone(url).Root)
            : SourceFileResolver.Resolve(argument);

    /// <summary>
    /// The semantic pass over a finished scan. What the reviewer rejects leaves the result, unless
    /// <c>--keep-rejected</c> asked for the rulings to be recorded without removing anything.
    /// </summary>
    private static async Task<ScanResult> ReviewAsync(
        PatternDetectionEngine engine,
        ScanResult result,
        CommandLineOptions options)
    {
        var model = LanguageModels.Create(options.VerifyModel, options.VerifyMaxTokens);
        Console.WriteLine($"Reviewing matches with {model.Name}...");

        var cache = FileVerdictCache.Load(options.VerifyCachePath!);
        var verifier = MatchReview.Create(
            model,
            engine.Detectors,
            cache,
            options.VerifyParallelism,
            options.KeepRejected);

        var reviewed = await verifier.VerifyAsync(result);
        cache.Save();

        Console.WriteLine($"{reviewed.Summary}\n");

        return reviewed.Scan;
    }

    private static void WriteTurtle(string? turtlePath, IGraph graph)
    {
        if (turtlePath is null)
            return;

        TurtleGraphWriter.Save(graph, turtlePath);
        Console.WriteLine($"Graph written to {turtlePath}.");
    }
}
