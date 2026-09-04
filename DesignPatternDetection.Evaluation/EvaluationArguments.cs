using System.Globalization;
using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Evaluation;

/// <summary>The harness's parsed command line.</summary>
/// <remarks>
/// The corpus argument may be a directory, a GitHub repository URL, the built-in names 'examples' or
/// 'refactoring-guru', or absent (the bundled DesignPatternExamples). Options:
/// <code>
///   --corpora [manifest.json]    evaluate every corpus in the manifest
///   --ground-truth file.json     explicit labels
///   --report out.json            write the report for later comparison
///   --baseline prev.json         compare against a previous report
///   --query-timeout seconds      per-detector query budget
///   --verify                     put every match to a language model before scoring it
///   --verify-model id            which model reviews (default claude-opus-5)
///   --verify-cache file.json     remember rulings between runs
///   --keep-rejected              record rulings without dropping anything
///   --verify-max-tokens n        per-adjudication ceiling
///   --verify-parallelism n       how many adjudications run at once
///   --analyze report.json        analyze a finished reviewed report. Replaces the corpus argument
/// </code>
/// </remarks>
public sealed record EvaluationArguments(
    string? Corpus,
    string? GroundTruth,
    string? Report,
    string? Baseline,
    double? QueryTimeout,
    bool Verify,
    string? VerifyModel,
    string? VerifyCache,
    bool KeepRejected = false,
    int? VerifyMaxTokens = null,
    int? VerifyParallelism = null,
    string? CorporaManifest = null,
    string? Analyze = null)
{
    public static EvaluationArguments Parse(string[] args)
    {
        string? corpus = null, groundTruth = null, report = null, baseline = null, corporaManifest = null;
        string? analyze = null;
        double? queryTimeout = null;
        var verify = false;
        string? verifyModel = null, verifyCache = null;
        var keepRejected = false;
        int? verifyMaxTokens = null;
        int? verifyParallelism = null;

        for (var i = 0; i < args.Length; i++)
            switch (args[i])
            {
                case "--ground-truth": groundTruth = OptionValue(args, ref i); break;
                case "--report": report = OptionValue(args, ref i); break;
                case "--baseline": baseline = OptionValue(args, ref i); break;
                case "--corpora":
                    corporaManifest = i + 1 < args.Length && !args[i + 1].StartsWith("--")
                        ? args[++i]
                        : DefaultManifest();
                    break;
                case "--analyze": analyze = OptionValue(args, ref i); break;
                case "--verify": verify = true; break;
                case "--verify-model": verifyModel = OptionValue(args, ref i); verify = true; break;
                case "--verify-cache": verifyCache = OptionValue(args, ref i); verify = true; break;
                case "--keep-rejected": keepRejected = true; verify = true; break;
                case "--verify-max-tokens":
                    var budget = OptionValue(args, ref i);
                    verifyMaxTokens = int.TryParse(budget, out var tokens) && tokens > 0
                        ? tokens
                        : throw new ArgumentException($"'--verify-max-tokens' needs a positive token count, got '{budget}'.");
                    verify = true;
                    break;
                case "--verify-parallelism":
                    var lanes = OptionValue(args, ref i);
                    verifyParallelism = int.TryParse(lanes, out var count) && count > 0
                        ? count
                        : throw new ArgumentException($"'--verify-parallelism' needs a positive count, got '{lanes}'.");
                    verify = true;
                    break;
                case "--query-timeout":
                    var value = OptionValue(args, ref i);
                    queryTimeout = double.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                        ? parsed
                        : throw new ArgumentException($"'--query-timeout' needs a positive number of seconds, got '{value}'.");
                    break;
                case var option when option.StartsWith("--"):
                    throw new ArgumentException($"Unknown option '{option}'.");
                case var positional when corpus is null: corpus = positional; break;
                default: throw new ArgumentException("Only one corpus argument is supported.");
            }

        if (analyze is not null && (corpus is not null || corporaManifest is not null || groundTruth is not null || verify))
            throw new ArgumentException(
                "'--analyze' reads a finished report and evaluates nothing, so it cannot be combined with a corpus, "
                + "'--corpora', '--ground-truth' or the review options.");

        if (corporaManifest is not null && corpus is not null)
            throw new ArgumentException(
                $"'--corpora' evaluates the whole manifest, so it cannot be combined with the corpus argument '{corpus}'.");

        if (corporaManifest is not null && groundTruth is not null)
            throw new ArgumentException("'--corpora' takes each corpus's labels from the manifest, so '--ground-truth' does not apply.");

        return new EvaluationArguments(
            corpus,
            groundTruth,
            report,
            baseline,
            queryTimeout,
            verify,
            verifyModel,
            verify ? verifyCache ?? FileVerdictCache.DefaultPath : null,
            keepRejected,
            verifyMaxTokens,
            verifyParallelism,
            corporaManifest,
            analyze);
    }

    /// <summary>
    /// The manifest <c>--corpora</c> falls back to, found by walking up from the running assembly's directory.
    /// </summary>
    private static string DefaultManifest()
    {
        const string relative = "evaluation-corpora/corpora.json";
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not locate the default corpus manifest '{relative}'. Pass a path: --corpora <manifest.json>.");
    }

    private static string OptionValue(string[] args, ref int i) => ++i < args.Length
        ? args[i]
        : throw new ArgumentException($"Option '{args[i - 1]}' needs a value.");
}
