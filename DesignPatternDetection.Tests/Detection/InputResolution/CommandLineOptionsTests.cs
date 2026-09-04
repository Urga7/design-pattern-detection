using DesignPatternDetection.Detection.InputResolution;
using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Tests.Detection.InputResolution;

public class CommandLineOptionsTests
{
    [Fact]
    public void Parses_positional_inputs_alongside_both_report_flags()
    {
        var options = CommandLineOptions.Parse(
            ["src", "--report", "out.json", "more.cs", "--sarif", "out.sarif"]);

        Assert.Equal(["src", "more.cs"], options.Inputs);
        Assert.Equal("out.json", options.ReportPath);
        Assert.Equal("out.sarif", options.SarifPath);
    }

    [Fact]
    public void Flags_only_leave_the_inputs_empty()
    {
        var options = CommandLineOptions.Parse(["--report", "out.json"]);

        Assert.Empty(options.Inputs);
        Assert.Equal("out.json", options.ReportPath);
        Assert.Null(options.SarifPath);
    }

    [Fact]
    public void Parses_query_and_turtle_paths()
    {
        var options = CommandLineOptions.Parse(["src", "--query", "shape.rq", "--turtle", "graph.ttl"]);

        Assert.Equal(["src"], options.Inputs);
        Assert.Equal("shape.rq", options.QueryPath);
        Assert.Equal("graph.ttl", options.TurtlePath);
    }

    [Fact]
    public void Rejects_query_combined_with_a_detector_report()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["--query", "shape.rq", "--report", "out.json"]));

        Assert.Contains("--query", exception.Message);
    }

    [Fact]
    public void Rejects_query_combined_with_verification()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["--query", "shape.rq", "--verify"]));

        Assert.Contains("--verify", exception.Message);
    }

    [Fact]
    public void Rejects_an_unknown_option()
    {
        var exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(["--nonsense"]));

        Assert.Contains("--nonsense", exception.Message);
    }

    [Fact]
    public void Rejects_an_option_without_a_value()
    {
        var exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(["--sarif"]));

        Assert.Contains("--sarif", exception.Message);
    }

    [Fact]
    public void Verification_is_off_and_unconfigured_by_default()
    {
        var options = CommandLineOptions.Parse(["src"]);

        Assert.False(options.Verify);
        Assert.Null(options.VerifyCachePath);
        Assert.Null(options.VerifyModel);
    }

    /// <summary>
    /// <c>--verify</c> alone defaults the cache path and leaves every other review setting to the provider's own
    /// default.
    /// </summary>
    [Fact]
    public void Verify_defaults_the_cache_path_and_nothing_else()
    {
        var options = CommandLineOptions.Parse(["src", "--verify"]);

        Assert.True(options.Verify);
        Assert.Equal(FileVerdictCache.DefaultPath, options.VerifyCachePath);
        Assert.Null(options.VerifyMaxTokens);
        Assert.Null(options.VerifyParallelism);
    }

    /// <summary>Naming a model, a cache or a parallelism turns review on, so the flag is never silently ignored.</summary>
    [Theory]
    [InlineData("--verify-model", "claude-sonnet-5")]
    [InlineData("--verify-cache", "verdicts.json")]
    [InlineData("--verify-parallelism", "16")]
    public void Configuring_review_turns_it_on(string option, string value)
    {
        Assert.True(CommandLineOptions.Parse(["src", option, value]).Verify);
    }

    [Fact]
    public void Keep_rejected_turns_review_on_without_removing_anything()
    {
        var options = CommandLineOptions.Parse(["src", "--keep-rejected"]);

        Assert.True(options.Verify);
        Assert.True(options.KeepRejected);
    }

    /// <summary>The ceiling covers reasoning and reply together, so a reasoning model may need more than the default.</summary>
    [Fact]
    public void The_adjudication_token_ceiling_can_be_raised()
    {
        var options = CommandLineOptions.Parse(["src", "--verify-max-tokens", "32000"]);

        Assert.True(options.Verify);
        Assert.Equal(32000, options.VerifyMaxTokens);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("0")]
    [InlineData("-1")]
    public void Rejects_a_token_ceiling_that_is_not_a_positive_count(string value)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["src", "--verify-max-tokens", value]));

        Assert.Contains("--verify-max-tokens", exception.Message);
    }

    /// <summary>Adjudication is network-bound, so how many run at once governs a large scan's wall time.</summary>
    [Fact]
    public void The_number_of_concurrent_adjudications_can_be_raised()
    {
        var options = CommandLineOptions.Parse(["src", "--verify-parallelism", "16"]);

        Assert.True(options.Verify);
        Assert.Equal(16, options.VerifyParallelism);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("0")]
    [InlineData("-1")]
    public void Rejects_a_parallelism_that_is_not_a_positive_count(string value)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["src", "--verify-parallelism", value]));

        Assert.Contains("--verify-parallelism", exception.Message);
    }
}
