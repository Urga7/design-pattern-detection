using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.Verification;
using DesignPatternDetection.Detection.Verification.LanguageModelImplementations;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// Evaluates one corpus, or every corpus in a manifest, against all detectors: discovers the labeled units, scans
/// each one in its own graph, and turns the results into precision/recall/F1. A combined run pools the per-corpus
/// reports at unit level rather than averaging their scores.
/// </summary>
public sealed class EvaluationRunner
{
    private readonly EvaluationArguments _arguments;
    private readonly CorpusResolver _corpora;
    private readonly MatchVerifier? _verifier;
    private readonly IReadOnlyList<string> _patternNames;
    private readonly CorpusLoader _loader;
    private readonly DetectorRunner _runner;
    private readonly PatternNameNormalizer _normalizer;
    private readonly int _detectorCount;

    /// <summary>The budget a corpus inherits when its manifest entry names none.</summary>
    private readonly TimeSpan _defaultQueryTimeout;

    public EvaluationRunner(
        EvaluationArguments arguments,
        IReadOnlyList<IPatternDetector> detectors,
        CorpusResolver corpora,
        MatchVerifier? verifier = null)
    {
        _arguments = arguments;
        _corpora = corpora;
        _verifier = verifier;
        _detectorCount = detectors.Count;
        _patternNames = detectors.Select(detector => detector.PatternName).ToList();
        _normalizer = new PatternNameNormalizer(_patternNames);
        _loader = new CorpusLoader(_normalizer);
        _runner = new DetectorRunner(detectors, verifier);

        // Captured before the first override, so one slow corpus cannot raise the ceiling for the rest of a run.
        _defaultQueryTimeout = arguments.QueryTimeout is { } seconds
            ? TimeSpan.FromSeconds(seconds)
            : SparqlPatternDetector.QueryTimeout;
    }

    public async Task<EvaluationReport> RunAsync() =>
        _arguments.CorporaManifest is { } manifestPath
            ? await EvaluateManifestAsync(manifestPath)
            : await EvaluateCorpusAsync(_arguments.Corpus, _arguments.GroundTruth, _defaultQueryTimeout);

    /// <summary>
    /// Evaluates every corpus in the manifest and pools them. Each corpus is cloned, scanned and deleted before the
    /// next begins, so a run holds one checkout at a time.
    /// </summary>
    private async Task<EvaluationReport> EvaluateManifestAsync(string path)
    {
        var manifest = CorpusManifest.Load(path);
        var reports = new List<EvaluationReport>();

        Console.WriteLine($"Evaluating {manifest.Corpora.Count} corpora from {path}.\n");

        foreach (var entry in manifest.Corpora)
        {
            Console.WriteLine($"--- {entry.Name}");

            var budget = entry.QueryTimeout is { } entrySeconds
                ? TimeSpan.FromSeconds(entrySeconds)
                : _defaultQueryTimeout;

            // Named from the manifest rather than the resolved source, so a local checkout is not labeled with a
            // temp path.
            var corpusReport = await EvaluateCorpusAsync(entry.Source, entry.GroundTruth, budget)
                               with { Corpus = entry.Name };

            ConsoleReportWriter.WriteCorpusScores(Console.Out, corpusReport);

            reports.Add(corpusReport);
            _corpora.Cleanup();
        }

        ConsoleReportWriter.WriteCorpusSummary(Console.Out, reports);
        Console.WriteLine();

        return MetricsCalculator.Combine("all corpora", reports) with { Corpora = reports };
    }

    /// <summary>One corpus, start to finish: resolve it, label its units, scan each, and score the results.</summary>
    private async Task<EvaluationReport> EvaluateCorpusAsync(
        string? corpusArgument,
        string? groundTruthPath,
        TimeSpan queryBudget)
    {
        SparqlPatternDetector.QueryTimeout = queryBudget;

        var (corpusName, corpusRoot, commit) = _corpora.Resolve(corpusArgument);

        var corpus = groundTruthPath is not null
            ? GroundTruth.Load(groundTruthPath, corpusRoot, _normalizer)
            : CorpusResolver.IsBundledExamples(corpusArgument)
                ? _loader.FromExampleFiles(corpusRoot)
                : _loader.FromLabeledFolders(corpusRoot);

        if (corpus.Units.Count == 0)
            throw new InvalidOperationException(
                $"No labeled units found in '{corpusName}'. Auto-derived labels need pattern-named " +
                "files or folders; use --ground-truth for anything else.");

        Console.WriteLine($"Evaluating {corpus.Units.Count} unit(s) with {_detectorCount} detector(s)...\n");

        var results = new List<UnitResult>();
        foreach (var unit in corpus.Units)
        {
            var result = await _runner.RunAsync(unit);
            results.Add(result);
            ConsoleReportWriter.WriteUnitProgress(Console.Out, result);
        }

        Console.WriteLine();

        return MetricsCalculator.Compute(
            corpusName,
            commit,
            _patternNames,
            results,
            corpus.SkippedUnlabeled,
            _verifier is null ? null : _arguments.VerifyModel ?? AnthropicLanguageModel.DefaultModel);
    }
}
