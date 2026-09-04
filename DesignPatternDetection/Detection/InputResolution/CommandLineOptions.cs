using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Detection.InputResolution;

/// <summary>
/// The detector CLI's arguments: any number of positional inputs (paths or GitHub repository URLs) plus optional
/// outputs. With no inputs the CLI scans the bundled examples.
/// </summary>
/// <remarks>
/// <c>--report</c>, <c>--sarif</c> and <c>--findings</c> write the matches as JSON, SARIF and RDF; <c>--turtle</c>
/// dumps the source graph they came from.
/// <c>--verify</c> adds the semantic pass: every candidate match is put to a language model with its pattern's
/// defining trait and the source of the types filling its roles, and matches the reviewer rejects leave the scan.
/// <c>--verify-model</c> names the reviewer, <c>--verify-max-tokens</c> caps one adjudication,
/// <c>--verify-parallelism</c> sets how many adjudications run at once, <c>--verify-cache</c> keeps rulings between
/// runs, and <c>--keep-rejected</c> records rulings without removing anything.
/// <c>--query</c> runs a SPARQL file against the graph instead of the detectors, and combines only with
/// <c>--turtle</c>.
/// </remarks>
public sealed record CommandLineOptions(
    IReadOnlyList<string> Inputs,
    string? ReportPath,
    string? SarifPath,
    string? QueryPath,
    string? TurtlePath,
    bool Verify = false,
    string? VerifyModel = null,
    string? VerifyCachePath = null,
    bool KeepRejected = false,
    int? VerifyMaxTokens = null,
    int? VerifyParallelism = null,
    string? FindingsPath = null)
{
    public static CommandLineOptions Parse(string[] args)
    {
        var inputs = new List<string>();
        string? reportPath = null;
        string? sarifPath = null;
        string? queryPath = null;
        string? turtlePath = null;
        string? findingsPath = null;
        var verify = false;
        string? verifyModel = null;
        string? verifyCachePath = null;
        var keepRejected = false;
        int? verifyMaxTokens = null;
        int? verifyParallelism = null;

        for (var i = 0; i < args.Length; i++)
            switch (args[i])
            {
                case "--report":
                    reportPath = OptionValue(args, ref i);
                    break;
                case "--sarif":
                    sarifPath = OptionValue(args, ref i);
                    break;
                case "--query":
                    queryPath = OptionValue(args, ref i);
                    break;
                case "--turtle":
                    turtlePath = OptionValue(args, ref i);
                    break;
                case "--findings":
                    findingsPath = OptionValue(args, ref i);
                    break;
                case "--verify":
                    verify = true;
                    break;
                case "--verify-model":
                    verifyModel = OptionValue(args, ref i);
                    verify = true;
                    break;
                case "--verify-cache":
                    verifyCachePath = OptionValue(args, ref i);
                    verify = true;
                    break;
                case "--keep-rejected":
                    keepRejected = true;
                    verify = true;
                    break;
                case "--verify-max-tokens":
                    var budget = OptionValue(args, ref i);
                    verifyMaxTokens = int.TryParse(budget, out var parsed) && parsed > 0
                        ? parsed
                        : throw new ArgumentException(
                            $"'--verify-max-tokens' needs a positive token count, got '{budget}'.");
                    verify = true;
                    break;
                case "--verify-parallelism":
                    var lanes = OptionValue(args, ref i);
                    verifyParallelism = int.TryParse(lanes, out var count) && count > 0
                        ? count
                        : throw new ArgumentException(
                            $"'--verify-parallelism' needs a positive count, got '{lanes}'.");
                    verify = true;
                    break;
                case var option when option.StartsWith("--"):
                    throw new ArgumentException($"Unknown option '{option}'.");
                case var input:
                    inputs.Add(input);
                    break;
            }

        if (queryPath is not null && (reportPath is not null || sarifPath is not null || findingsPath is not null))
            throw new ArgumentException(
                "--query replaces the detector run, so --report/--sarif/--findings cannot be combined with it.");

        if (queryPath is not null && verify)
            throw new ArgumentException("--query replaces the detector run, so there are no matches for --verify to review.");

        return new CommandLineOptions(
            inputs,
            reportPath,
            sarifPath,
            queryPath,
            turtlePath,
            verify,
            verifyModel,
            verify ? verifyCachePath ?? FileVerdictCache.DefaultPath : null,
            keepRejected,
            verifyMaxTokens,
            verifyParallelism,
            findingsPath);
    }

    private static string OptionValue(string[] args, ref int i) =>
        ++i < args.Length ? args[i] : throw new ArgumentException($"Option '{args[i - 1]}' needs a value.");
}