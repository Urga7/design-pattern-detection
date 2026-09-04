using DesignPatternDetection.Detection;
using DesignPatternDetection.Evaluation;

namespace DesignPatternDetection.Tests.Evaluation;

public class PatternNameNormalizerTests
{
    private readonly PatternNameNormalizer _normalizer =
        new(PatternDetectionEngine.DiscoverDetectors().Select(detector => detector.PatternName));

    [Fact]
    public void Every_canonical_detector_name_round_trips()
    {
        foreach (var name in _normalizer.CanonicalNames)
            Assert.Equal(name, _normalizer.Normalize(name));
    }

    [Fact]
    public void Concatenated_pascal_case_matches_the_spaced_canonical_name()
    {
        Assert.Equal("Chain of Responsibility", _normalizer.Normalize("ChainOfResponsibility"));
        Assert.Equal("Factory Method", _normalizer.Normalize("FactoryMethod"));
        Assert.Equal("Template Method", _normalizer.Normalize("TemplateMethod"));
    }

    [Fact]
    public void Matching_ignores_case_and_non_letter_characters()
    {
        Assert.Equal("Factory Method", _normalizer.Normalize("factory_method"));
        Assert.Equal("Abstract Factory", _normalizer.Normalize("abstractfactory"));
    }

    [Fact]
    public void A_dotted_name_matches_through_its_pattern_segment()
    {
        Assert.Equal("Abstract Factory", _normalizer.NormalizeDottedName("AbstractFactory.Conceptual"));
        Assert.Equal(
            "Template Method",
            _normalizer.NormalizeDottedName("RefactoringGuru.DesignPatterns.TemplateMethod.Conceptual"));
    }

    [Fact]
    public void Unknown_names_normalize_to_null()
    {
        Assert.Null(_normalizer.Normalize("Utilities"));
        Assert.Null(_normalizer.Normalize("123"));
        Assert.Null(_normalizer.Normalize(""));
        Assert.Null(_normalizer.NormalizeDottedName("Common.Helpers"));
    }
}
