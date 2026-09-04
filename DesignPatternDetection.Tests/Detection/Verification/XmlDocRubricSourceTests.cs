using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Tests.Detection.Verification;

/// <summary>
/// Guards the link between a detector's documentation and the rubric a reviewer judges against, which breaks
/// silently: the project stops emitting its XML documentation file, or a detector's <c>&lt;remarks&gt;</c> is
/// deleted, and adjudication just gets blander.
/// </summary>
public class XmlDocRubricSourceTests
{
    private static readonly IReadOnlyList<IPatternDetector> Detectors =
        new PatternDetectionEngine().Detectors;

    [Fact]
    public void Every_detector_has_a_rubric()
    {
        var rubrics = XmlDocRubricSource.Load(Detectors);

        var missing = Detectors
            .Where(detector => string.IsNullOrWhiteSpace(rubrics.Rubric(detector.PatternName)))
            .Select(detector => detector.PatternName)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// The rubric must carry the discriminating trait and name the neighbouring shapes, not just restate the
    /// pattern's name.
    /// </summary>
    [Fact]
    public void The_rubric_states_the_defining_trait_and_its_neighbours()
    {
        var rubric = XmlDocRubricSource.Load(Detectors).Rubric("Decorator");

        Assert.NotNull(rubric);
        Assert.Contains("self-wrapping", rubric);
        Assert.Contains("Adapter", rubric);
        Assert.Contains("Composite", rubric);
    }

    [Fact]
    public void Markup_is_rendered_as_prose()
    {
        var rubric = XmlDocRubricSource.Load(Detectors).Rubric("Composite")!;

        // No angle brackets from <c>/<em>, and cref targets keep only their
        // readable tail rather than a namespace-qualified symbol.
        Assert.DoesNotContain("<c>", rubric);
        Assert.DoesNotContain("<em>", rubric);
        Assert.DoesNotContain("T:DesignPatternDetection", rubric);
    }

    [Fact]
    public void Hard_wrapping_in_the_source_comment_does_not_survive()
    {
        var rubric = XmlDocRubricSource.Load(Detectors).Rubric("Composite")!;

        // Paragraph breaks are kept; the comment's own line wrapping is not,
        // so the reviewer reads sentences rather than a column of fragments.
        Assert.DoesNotContain("\n ", rubric);
        Assert.All(rubric.Split("\n\n"), paragraph => Assert.DoesNotContain("\n", paragraph));
    }

    [Fact]
    public void An_unknown_pattern_has_no_rubric()
    {
        Assert.Null(XmlDocRubricSource.Load(Detectors).Rubric("Nonexistent"));
    }
}
