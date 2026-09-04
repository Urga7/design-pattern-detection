using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Tests.Detection.Verification;

/// <summary>
/// The cache is what makes a reviewed run reproducible and a re-run free, so what does and does not invalidate an
/// entry is a correctness property, not an optimisation detail. Every field that could change a ruling must be in the
/// key; anything else in the key would silently re-bill an unchanged corpus.
/// </summary>
public class VerdictCacheTests
{
    private const string Prompt = "PATTERN: Decorator\nThe decorator wraps its own abstraction.";
    private const string Source = "public sealed class Wrapper { }";

    private static readonly string[] Roles = ["Demo.Wrapper", "Demo.Component"];

    /// <summary>
    /// The rubric is read from each detector's own documentation, so rewording that documentation changes how matches
    /// are judged. Without the prompt in the key, the next run would answer from rulings the old wording produced.
    /// </summary>
    [Fact]
    public void Rewording_the_prompt_invalidates_the_ruling()
    {
        Assert.NotEqual(
            FileVerdictCache.Key("m", Prompt, "Decorator", Roles, Source),
            FileVerdictCache.Key("m", Prompt + " It forwards to the wrapped instance.", "Decorator", Roles, Source));
    }

    [Theory]
    [InlineData("other-model", Prompt, "Decorator", Source)]
    [InlineData("m", Prompt, "Proxy", Source)]
    [InlineData("m", Prompt, "Decorator", "public sealed class Wrapper { void Run() { } }")]
    public void Every_other_input_invalidates_it_too(string model, string prompt, string pattern, string source)
    {
        Assert.NotEqual(
            FileVerdictCache.Key("m", Prompt, "Decorator", Roles, Source),
            FileVerdictCache.Key(model, prompt, pattern, Roles, source));
    }

    [Fact]
    public void Role_order_does_not_change_the_key()
    {
        Assert.Equal(
            FileVerdictCache.Key("m", Prompt, "Decorator", Roles, Source),
            FileVerdictCache.Key("m", Prompt, "Decorator", Roles.Reverse(), Source));
    }

    [Fact]
    public void A_missing_cache_file_loads_empty_rather_than_failing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dpd-absent-{Guid.NewGuid():N}.json");

        var cache = FileVerdictCache.Load(path);

        Assert.False(cache.TryGet("anything", out _));
    }

    [Fact]
    public void A_corrupt_cache_file_loads_empty_rather_than_failing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dpd-corrupt-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ this is not json");

        try
        {
            Assert.False(FileVerdictCache.Load(path).TryGet("anything", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rulings_survive_a_save_and_reload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dpd-roundtrip-{Guid.NewGuid():N}.json");
        var key = FileVerdictCache.Key("m", Prompt, "Decorator", Roles, Source);

        try
        {
            var written = FileVerdictCache.Load(path);
            written.Set(key, new MatchVerdict(VerificationOutcome.Rejected, "Wraps a foreign class.", "m"));
            written.Save();

            Assert.True(FileVerdictCache.Load(path).TryGet(key, out var reloaded));
            Assert.Equal(VerificationOutcome.Rejected, reloaded.Outcome);
            Assert.Equal("Wraps a foreign class.", reloaded.Rationale);
            Assert.Equal("m", reloaded.Model);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
