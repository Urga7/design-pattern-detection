using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// The harness's command line, end to end: parses the arguments, builds the reviewer when one was asked for,
/// evaluates the corpus or the whole manifest, prints the scoreboard, and deletes every clone on the way out. A run
/// whose micro F1 fell below its <c>--baseline</c> exits 1.
/// </summary>
public static class EvaluationCli
{
    /// <summary>Runs one invocation and returns the process exit code.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        var corpora = new CorpusResolver();
        Console.CancelKeyPress += (_, _) => corpora.Cleanup();

        try
        {
            return await ExecuteAsync(EvaluationArguments.Parse(args), corpora);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
        finally
        {
            corpora.Cleanup();
        }
    }

    private static async Task<int> ExecuteAsync(EvaluationArguments arguments, CorpusResolver corpora)
    {
        if (arguments.Analyze is { } reportPath)
        {
            ConsoleAnalysisWriter.Write(Console.Out, EvaluationReport.Load(reportPath));
            return 0;
        }

        var detectors = PatternDetectionEngine.DiscoverDetectors();

        // One cache across every corpus: rulings are keyed by content, so a type
        // that appears in two units - or two corpora - is adjudicated once.
        FileVerdictCache? verdictCache = null;
        MatchVerifier? verifier = null;

        if (arguments.Verify)
        {
            var reviewer = LanguageModels.Create(arguments.VerifyModel, arguments.VerifyMaxTokens);
            verdictCache = FileVerdictCache.Load(arguments.VerifyCache!);
            verifier = MatchReview.Create(
                reviewer,
                detectors,
                verdictCache,
                arguments.VerifyParallelism,
                arguments.KeepRejected);

            Console.WriteLine($"Reviewing matches with {reviewer.Name}.\n");
        }

        var report = await new EvaluationRunner(arguments, detectors, corpora, verifier).RunAsync();

        verdictCache?.Save();

        var comparison = arguments.Baseline is not null
            ? BaselineComparer.Compare(report, EvaluationReport.Load(arguments.Baseline))
            : null;

        ConsoleReportWriter.Write(Console.Out, report, comparison);

        if (arguments.Report is not null)
        {
            report.Save(arguments.Report);
            Console.WriteLine($"\nReport written to {arguments.Report}.");
        }

        return comparison?.HasRegression == true ? 1 : 0;
    }
}
