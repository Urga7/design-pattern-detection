using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Tests.Detection.Verification;

public class MatchVerifierTests
{
    [Fact]
    public async Task Rejected_matches_leave_the_scan()
    {
        var scan = ScanOf(("Decorator", "Demo.Wrapper"));
        var model = new ScriptedModel(Reply("rejected", "Wrapper stores no Component."));

        var result = await Verifier(model).VerifyAsync(scan);

        Assert.Empty(result.Scan.Detections.Single().Matches);
        Assert.Equal(1, result.Summary.Rejected);
        Assert.Equal(1, result.Summary.Dropped);
    }

    [Fact]
    public async Task Confirmed_matches_stay_and_carry_the_verdict()
    {
        var scan = ScanOf(("Decorator", "Demo.Wrapper"));
        var model = new ScriptedModel(Reply("confirmed", "Wrapper forwards through _inner."));

        var result = await Verifier(model).VerifyAsync(scan);

        var match = Assert.Single(result.Scan.Detections.Single().Matches);
        Assert.Equal(VerificationOutcome.Confirmed, match.Verdict!.Outcome);
        Assert.Equal("Wrapper forwards through _inner.", match.Verdict.Rationale);
        Assert.Equal("scripted", match.Verdict.Model);
        Assert.Equal(0, result.Summary.Dropped);
    }

    [Fact]
    public async Task Uncertain_matches_are_kept()
    {
        var scan = ScanOf(("Decorator", "Demo.Wrapper"));
        var model = new ScriptedModel(Reply("uncertain", "The Component is declared elsewhere."));

        var result = await Verifier(model).VerifyAsync(scan);

        Assert.Single(result.Scan.Detections.Single().Matches);
        Assert.Equal(1, result.Summary.Uncertain);
        Assert.Equal(0, result.Summary.Dropped);
    }

    /// <summary>
    /// An unreachable reviewer must look like "not reviewed", never like a wave of rejections.
    /// </summary>
    [Fact]
    public async Task A_failing_reviewer_keeps_every_match_and_reports_why()
    {
        var scan = ScanOf(("Decorator", "Demo.Wrapper"), ("Proxy", "Demo.Guard"));
        var model = new FailingModel("401 Unauthorized");

        var result = await Verifier(model).VerifyAsync(scan);

        Assert.Equal(2, result.Scan.Detections.Sum(detection => detection.Matches.Count));
        Assert.All(result.Scan.Detections.SelectMany(detection => detection.Matches),
            match => Assert.Null(match.Verdict));
        Assert.Equal(2, result.Summary.Unreviewed);
        Assert.Equal(0, result.Summary.Dropped);
        Assert.Equal("401 Unauthorized", result.Summary.FirstFailure);
    }

    [Fact]
    public async Task Keeping_rejected_matches_annotates_without_removing()
    {
        var scan = ScanOf(("Decorator", "Demo.Wrapper"));
        var model = new ScriptedModel(Reply("rejected", "Not a decorator."));

        var result = await Verifier(model, new VerificationOptions(DropRejected: false)).VerifyAsync(scan);

        var match = Assert.Single(result.Scan.Detections.Single().Matches);
        Assert.Equal(VerificationOutcome.Rejected, match.Verdict!.Outcome);
        Assert.Equal(0, result.Summary.Dropped);
    }

    [Fact]
    public async Task A_cached_ruling_is_reused_instead_of_asking_again()
    {
        var cache = new InMemoryVerdictCache();
        var model = new ScriptedModel(Reply("rejected", "Not a decorator."));

        var first = await Verifier(model, cache: cache).VerifyAsync(ScanOf(("Decorator", "Demo.Wrapper")));
        var second = await Verifier(model, cache: cache).VerifyAsync(ScanOf(("Decorator", "Demo.Wrapper")));

        Assert.Equal(1, model.Calls);
        Assert.Equal(0, first.Summary.CacheHits);
        Assert.Equal(1, second.Summary.CacheHits);
        Assert.Empty(second.Scan.Detections.Single().Matches);
    }

    /// <summary>
    /// A different reviewer must not inherit the previous one's rulings - the model is part of a verdict's identity.
    /// </summary>
    [Fact]
    public async Task Changing_the_model_invalidates_cached_rulings()
    {
        var cache = new InMemoryVerdictCache();

        await Verifier(new ScriptedModel(Reply("rejected", "no")), cache: cache)
            .VerifyAsync(ScanOf(("Decorator", "Demo.Wrapper")));

        var other = new ScriptedModel(Reply("confirmed", "yes")) { Name = "other-model" };
        var result = await Verifier(other, cache: cache).VerifyAsync(ScanOf(("Decorator", "Demo.Wrapper")));

        Assert.Equal(1, other.Calls);
        Assert.Equal(0, result.Summary.CacheHits);
        Assert.Single(result.Scan.Detections.Single().Matches);
    }

    [Fact]
    public async Task A_match_with_no_readable_source_is_left_alone()
    {
        // A role bound to a metadata-only type has no span, so there is nothing
        // to put in front of a reviewer.
        var match = new PatternMatch(
            "Adapter",
            new Dictionary<string, string> { ["adaptee"] = "IEnumerable" },
            new Dictionary<string, string> { ["adaptee"] = "System.Collections.IEnumerable" });

        var scan = new ScanResult(
            1,
            [new PatternDetection("Adapter", [match])],
            new SourceGraph(new VDS.RDF.Graph(), new Dictionary<string, SourceSpan>()));

        var model = new ScriptedModel(Reply("rejected", "should never be asked"));
        var result = await Verifier(model).VerifyAsync(scan);

        Assert.Equal(0, model.Calls);
        Assert.Single(result.Scan.Detections.Single().Matches);
        Assert.Equal(1, result.Summary.Unreviewed);
    }

    [Fact]
    public async Task An_unparsable_reply_counts_as_uncertain_rather_than_failing_the_scan()
    {
        var scan = ScanOf(("Decorator", "Demo.Wrapper"));
        var model = new ScriptedModel("I could not decide.");

        var result = await Verifier(model).VerifyAsync(scan);

        var match = Assert.Single(result.Scan.Detections.Single().Matches);
        Assert.Equal(VerificationOutcome.Uncertain, match.Verdict!.Outcome);
    }

    [Fact]
    public async Task The_rubric_and_the_role_source_reach_the_reviewer()
    {
        var scan = ScanOf(("Decorator", "Demo.Wrapper"));
        var model = new ScriptedModel(Reply("confirmed", "ok"));

        await Verifier(model).VerifyAsync(scan);

        var request = Assert.Single(model.Requests);
        Assert.Contains("PATTERN: Decorator", request.SystemPrompt);
        Assert.Contains("wraps its own abstraction", request.SystemPrompt);
        Assert.Contains("component = Wrapper", request.UserPrompt);
        Assert.Contains("class Wrapper", request.UserPrompt);
    }

    /// <summary>A reviewed match carries its token cost into the summary.</summary>
    [Fact]
    public async Task Token_usage_and_duration_reach_the_summary()
    {
        var scan = ScanOf(("Decorator", "Demo.Wrapper"), ("Proxy", "Demo.Guard"));
        var model = new ScriptedModel(Reply("confirmed", "ok"));

        var result = await Verifier(model).VerifyAsync(scan);

        Assert.Equal(200, result.Summary.InputTokens);
        Assert.Equal(40, result.Summary.OutputTokens);
        Assert.True(result.Summary.Duration > TimeSpan.Zero);
    }

    /// <summary>A cached ruling adds no tokens to the reported spend.</summary>
    [Fact]
    public async Task A_cached_ruling_adds_no_tokens()
    {
        var cache = new InMemoryVerdictCache();
        var model = new ScriptedModel(Reply("confirmed", "ok"));

        await Verifier(model, cache: cache).VerifyAsync(ScanOf(("Decorator", "Demo.Wrapper")));
        var second = await Verifier(model, cache: cache).VerifyAsync(ScanOf(("Decorator", "Demo.Wrapper")));

        Assert.Equal(1, second.Summary.CacheHits);
        Assert.Equal(0, second.Summary.InputTokens);
        Assert.Equal(0, second.Summary.OutputTokens);
    }

    private static MatchVerifier Verifier(
        ILanguageModel model,
        VerificationOptions? options = null,
        IVerdictCache? cache = null) =>
        new(model, new StubRubrics(), cache, options);

    private static string Reply(string verdict, string rationale) =>
        $$"""{"verdict": "{{verdict}}", "rationale": "{{rationale}}"}""";

    /// <summary>
    /// A scan whose roles point at a real file, so the verifier has something to read. Each entry becomes one
    /// pattern with one single-role match.
    /// </summary>
    private static ScanResult ScanOf(params (string Pattern, string Fragment)[] entries)
    {
        var locations = new Dictionary<string, SourceSpan>();
        var detections = new List<PatternDetection>();

        foreach (var (pattern, fragment) in entries)
        {
            var simpleName = fragment[(fragment.LastIndexOf('.') + 1)..];
            var path = Path.Combine(Path.GetTempPath(), $"dpd-verify-{simpleName}.cs");
            File.WriteAllText(path, $"public sealed class {simpleName}\n{{\n    public void Run() {{ }}\n}}\n");

            locations[fragment] = new SourceSpan(path, 1, 1);
            detections.Add(new PatternDetection(pattern, [
                new PatternMatch(
                    pattern,
                    new Dictionary<string, string> { ["component"] = simpleName },
                    new Dictionary<string, string> { ["component"] = fragment })
            ]));
        }

        return new ScanResult(entries.Length, detections, new SourceGraph(new VDS.RDF.Graph(), locations));
    }

    private sealed class StubRubrics : IRubricSource
    {
        public string? Rubric(string patternName) =>
            patternName == "Decorator" ? "The decorator wraps its own abstraction and forwards to it." : null;
    }

    private sealed class ScriptedModel(string reply) : ILanguageModel
    {
        public List<LanguageModelRequest> Requests { get; } = [];

        public string Name { get; init; } = "scripted";

        public int Calls => Requests.Count;

        public Task<LanguageModelReply> CompleteAsync(
            LanguageModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new LanguageModelReply(reply, 100, 20));
        }
    }

    private sealed class FailingModel(string message) : ILanguageModel
    {
        public string Name => "failing";

        public Task<LanguageModelReply> CompleteAsync(
            LanguageModelRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(message);
    }

    private sealed class InMemoryVerdictCache : IVerdictCache
    {
        private readonly Dictionary<string, MatchVerdict> _entries = [];

        public bool TryGet(string key, out MatchVerdict verdict) => _entries.TryGetValue(key, out verdict!);

        public void Set(string key, MatchVerdict verdict) => _entries[key] = verdict;
    }
}
