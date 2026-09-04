using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DesignPatternDetection.Detection.Verification;

/// <summary>How a reviewed scan is assembled.</summary>
/// <param name="MaxParallelism">How many candidates may be in flight at once.</param>
/// <param name="DropRejected">
/// Whether a rejected match leaves the scan. False keeps every match and only annotates it.
/// </param>
/// <param name="MaxSourceLines">Ceiling on the excerpt taken for one role.</param>
public sealed record VerificationOptions(
    int MaxParallelism = 8,
    bool DropRejected = true,
    int MaxSourceLines = 400);

/// <summary>
/// What one reviewed scan cost and changed. <c>FirstFailure</c> carries the message of the first failed adjudication,
/// when there was one.
/// </summary>
public sealed record VerificationSummary(
    int Reviewed,
    int Confirmed,
    int Uncertain,
    int Rejected,
    int Dropped,
    int Unreviewed,
    int CacheHits,
    string? FirstFailure = null,
    long InputTokens = 0,
    long OutputTokens = 0,
    TimeSpan Duration = default)
{
    /// <summary>Sums two tallies, so a harness that reviews unit by unit can report one figure for the corpus.</summary>
    public static VerificationSummary operator +(VerificationSummary left, VerificationSummary right) =>
        new(left.Reviewed + right.Reviewed,
            left.Confirmed + right.Confirmed,
            left.Uncertain + right.Uncertain,
            left.Rejected + right.Rejected,
            left.Dropped + right.Dropped,
            left.Unreviewed + right.Unreviewed,
            left.CacheHits + right.CacheHits,
            left.FirstFailure ?? right.FirstFailure,
            left.InputTokens + right.InputTokens,
            left.OutputTokens + right.OutputTokens,
            left.Duration + right.Duration);

    public override string ToString()
    {
        var line = $"reviewed {Reviewed} match(es): {Confirmed} confirmed, {Uncertain} uncertain, "
                   + $"{Rejected} rejected ({Dropped} dropped), {Unreviewed} unreviewed, {CacheHits} from cache";

        if (InputTokens > 0 || OutputTokens > 0)
            line += $"; {InputTokens} in / {OutputTokens} out tokens in {Duration.TotalSeconds:0.0}s";

        return FirstFailure is { Length: > 0 } ? $"{line}\n  First failure: {FirstFailure}" : line;
    }
}

/// <summary>A reviewed scan and the tally of what the review did to it.</summary>
public sealed record VerificationResult(ScanResult Scan, VerificationSummary Summary);

/// <summary>
/// The semantic pass over a completed scan: every candidate match is put to a language model together with its
/// pattern's defining trait and the source of the types filling its roles, and the ruling is attached to the match.
/// </summary>
public sealed class MatchVerifier(
    ILanguageModel model,
    IRubricSource rubrics,
    IVerdictCache? cache = null,
    VerificationOptions? options = null)
{
    /// <summary>The reply contract.</summary>
    private const string VerdictSchema = """
        {
          "type": "object",
          "properties": {
            "verdict": { "type": "string", "enum": ["confirmed", "rejected", "uncertain"] },
            "rationale": { "type": "string" }
          },
          "required": ["verdict", "rationale"],
          "additionalProperties": false
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IVerdictCache _cache = cache ?? new NullVerdictCache();
    private readonly VerificationOptions _options = options ?? new VerificationOptions();
    private readonly Lock _cacheLock = new();
    private readonly ConcurrentDictionary<string, string> _prompts = new();

    public async Task<VerificationResult> VerifyAsync(ScanResult scan, CancellationToken cancellationToken = default)
    {
        var excerpts = new SourceExcerptReader(_options.MaxSourceLines);
        var gate = new SemaphoreSlim(_options.MaxParallelism);
        var tally = new Tally();
        var started = Stopwatch.GetTimestamp();

        var detections = await Task.WhenAll(scan.Detections.Select(async detection =>
        {
            var reviewed = await Task.WhenAll(detection.Matches.Select(async match =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    return await ReviewAsync(match, scan, excerpts, tally, cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }));

            var surviving = reviewed
                .Where(match => !(_options.DropRejected && match.Verdict?.Outcome == VerificationOutcome.Rejected))
                .ToList();

            Interlocked.Add(ref tally.Dropped, reviewed.Length - surviving.Count);

            return new PatternDetection(detection.PatternName, surviving);
        }));

        return new VerificationResult(
            scan with { Detections = detections },
            tally.ToSummary(Stopwatch.GetElapsedTime(started)));
    }

    private async Task<PatternMatch> ReviewAsync(
        PatternMatch match,
        ScanResult scan,
        SourceExcerptReader excerpts,
        Tally tally,
        CancellationToken cancellationToken)
    {
        var (roles, sourceText) = Describe(match, scan, excerpts);

        // A match whose roles all resolve to metadata-only types has nothing to read, so it is left as found.
        if (sourceText.Length == 0)
        {
            Interlocked.Increment(ref tally.Unreviewed);
            return match;
        }

        var systemPrompt = PromptFor(match.PatternName);
        var fragments = match.Fragments?.Values ?? [];
        var key = FileVerdictCache.Key(model.Name, systemPrompt, match.PatternName, fragments, sourceText);

        lock (_cacheLock)
        {
            if (_cache.TryGet(key, out var cached))
            {
                Interlocked.Increment(ref tally.CacheHits);
                Count(tally, cached.Outcome);
                return match with { Verdict = cached };
            }
        }

        MatchVerdict verdict;
        try
        {
            var reply = await model.CompleteAsync(
                new LanguageModelRequest(systemPrompt, roles + "\n\n" + sourceText, VerdictSchema),
                cancellationToken);

            Interlocked.Add(ref tally.InputTokens, reply.InputTokens);
            Interlocked.Add(ref tally.OutputTokens, reply.OutputTokens);

            verdict = Parse(reply.Text, model.Name);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A failed adjudication counts as unreviewed, never as a rejection.
            Interlocked.Increment(ref tally.Unreviewed);
            Interlocked.CompareExchange(ref tally.FirstFailure, exception.Message, null);
            return match;
        }

        lock (_cacheLock)
        {
            _cache.Set(key, verdict);
        }

        Count(tally, verdict.Outcome);
        return match with { Verdict = verdict };
    }

    /// <summary>The preamble for a pattern, built once per pattern and reused across the scan.</summary>
    private string PromptFor(string patternName) => _prompts.GetOrAdd(patternName, SystemPrompt);

    /// <summary>
    /// The per-pattern preamble: the task, the pattern's defining trait - its rubric, or the Gang of Four definition
    /// when no rubric is available - and what each of the three verdicts means.
    /// </summary>
    private string SystemPrompt(string patternName)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine(
            "You are adjudicating a candidate design-pattern match produced by a static analyser. "
            + "Decide whether the claimed role assignment is correct.");
        prompt.AppendLine();
        prompt.AppendLine($"PATTERN: {patternName}");
        prompt.AppendLine();
        prompt.AppendLine("DEFINING TRAIT");
        prompt.AppendLine(
            rubrics.Rubric(patternName) is { Length: > 0 } rubric
                ? rubric
                : $"Apply the Gang of Four definition of {patternName}: the participants must collaborate as that "
                  + "pattern describes, not merely resemble its class shape.");
        prompt.AppendLine();
        prompt.AppendLine("DECIDE");
        prompt.AppendLine(
            "confirmed - the trait holds. Name the member that carries it.");
        prompt.AppendLine(
            "rejected - the trait fails. Name the member or relationship that breaks it.");
        prompt.AppendLine(
            "uncertain - participants are declared outside the supplied source, or the code genuinely admits "
            + "both readings.");
        prompt.AppendLine();
        prompt.AppendLine(
            "Judge only the roles as assigned. A different, better assignment among the same types is still a "
            + "rejection of this one. Keep the rationale to one sentence naming specific code.");

        return prompt.ToString();
    }

    /// <summary>
    /// Describes the candidate: which type fills which role, and the source of each role that resolves to a
    /// declaration in the scanned code.
    /// </summary>
    private static (string Roles, string Source) Describe(
        PatternMatch match,
        ScanResult scan,
        SourceExcerptReader excerpts)
    {
        var roles = new StringBuilder("CANDIDATE");
        var source = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        roles.AppendLine();

        foreach (var (role, label) in match.Bindings)
        {
            var span = match.Fragments is { } fragments
                       && fragments.TryGetValue(role, out var fragment)
                       && scan.Locations.TryGetValue(fragment, out var found)
                ? found
                : null;

            roles.AppendLine(span is null
                ? $"  {role} = {label}"
                : $"  {role} = {label}  ({Relative(span.FilePath)}:{span.StartLine})");

            // Roles often share a declaration; each is sent once.
            if (span is null || !seen.Add($"{span.FilePath}:{span.StartLine}"))
                continue;

            if (excerpts.Read(span) is not { Length: > 0 } text) continue;
            
            source.AppendLine($"--- {Relative(span.FilePath)}:{span.StartLine} ---");
            source.AppendLine(text);
            source.AppendLine();
        }

        return (roles.ToString().TrimEnd(), source.Length == 0 ? "" : "SOURCE\n" + source.ToString().TrimEnd());
    }

    private static string Relative(string path)
    {
        try
        {
            var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
            return relative.StartsWith("..", StringComparison.Ordinal) ? Path.GetFileName(path) : relative;
        }
        catch (ArgumentException)
        {
            return Path.GetFileName(path);
        }
    }

    /// <summary>Reads the reply.</summary>
    /// <remarks>An unparsable or unknown verdict counts as <see cref="VerificationOutcome.Uncertain"/>.</remarks>
    private static MatchVerdict Parse(string reply, string modelName)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<VerdictPayload>(ExtractJson(reply), JsonOptions);

            var outcome = payload?.Verdict?.ToLowerInvariant() switch
            {
                "confirmed" => VerificationOutcome.Confirmed,
                "rejected" => VerificationOutcome.Rejected,
                _ => VerificationOutcome.Uncertain
            };

            return new MatchVerdict(outcome, payload?.Rationale?.Trim() ?? "", modelName);
        }
        catch (JsonException)
        {
            return new MatchVerdict(VerificationOutcome.Uncertain, "The reviewer's reply could not be parsed.", modelName);
        }
    }

    /// <summary>The outermost JSON object of a reply, so one wrapped in prose or a fenced block still parses.</summary>
    private static string ExtractJson(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');

        return start >= 0 && end > start ? reply[start..(end + 1)] : reply;
    }

    private static void Count(Tally tally, VerificationOutcome outcome)
    {
        Interlocked.Increment(ref tally.Reviewed);

        switch (outcome)
        {
            case VerificationOutcome.Confirmed:
                Interlocked.Increment(ref tally.Confirmed);
                break;
            case VerificationOutcome.Rejected:
                Interlocked.Increment(ref tally.Rejected);
                break;
            default:
                Interlocked.Increment(ref tally.Uncertain);
                break;
        }
    }

    private sealed record VerdictPayload(string? Verdict, string? Rationale);

    private sealed class Tally
    {
        public int Reviewed;
        public int Confirmed;
        public int Uncertain;
        public int Rejected;
        public int Dropped;
        public int Unreviewed;
        public int CacheHits;
        public string? FirstFailure;
        public long InputTokens;
        public long OutputTokens;

        public VerificationSummary ToSummary(TimeSpan duration) =>
            new(Reviewed, Confirmed, Uncertain, Rejected, Dropped, Unreviewed, CacheHits, FirstFailure,
                InputTokens, OutputTokens, duration);
    }
}
