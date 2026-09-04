using DesignPatternDetection.Evaluation;

namespace DesignPatternDetection.Tests.Evaluation;

public class GroundTruthTests
{
    private static readonly PatternNameNormalizer Normalizer =
        new(["Decorator", "Builder", "Factory Method"]);

    [Fact]
    public void Loads_units_with_normalized_pattern_names()
    {
        using var fixture = new TempDirectory("dpd-groundtruth-tests-");
        var source = fixture.Write(Path.Combine("src", "Loggers", "Logger.cs"));
        var groundTruth = fixture.Write("labels.json", """
        { "units": [ { "path": "src/Loggers", "patterns": ["decorator", "FactoryMethod"] } ] }
        """);

        var corpus = GroundTruth.Load(groundTruth, fixture.Root, Normalizer);

        var unit = Assert.Single(corpus.Units);
        Assert.Equal("src/Loggers", unit.Name);
        Assert.Equal([source], unit.Files);
        Assert.Equal(new HashSet<string> { "Decorator", "Factory Method" }, unit.ExpectedPatterns);
    }

    [Fact]
    public void Backslash_paths_and_single_file_units_resolve()
    {
        using var fixture = new TempDirectory("dpd-groundtruth-tests-");
        var source = fixture.Write(Path.Combine("src", "Widget.cs"));
        var groundTruth = fixture.Write("labels.json", """
        { "units": [ { "path": "src\\Widget.cs", "patterns": ["Builder"] } ] }
        """);

        var unit = Assert.Single(GroundTruth.Load(groundTruth, fixture.Root, Normalizer).Units);
        Assert.Equal([source], unit.Files);
    }

    [Fact]
    public void An_empty_patterns_array_is_a_deliberate_negative_unit()
    {
        using var fixture = new TempDirectory("dpd-groundtruth-tests-");
        fixture.Write(Path.Combine("src", "Helpers", "Helper.cs"));
        var groundTruth = fixture.Write("labels.json", """
        { "units": [ { "path": "src/Helpers", "patterns": [] } ] }
        """);

        var unit = Assert.Single(GroundTruth.Load(groundTruth, fixture.Root, Normalizer).Units);
        Assert.Empty(unit.ExpectedPatterns);
    }

    [Fact]
    public void An_unknown_pattern_name_fails_the_load()
    {
        using var fixture = new TempDirectory("dpd-groundtruth-tests-");
        fixture.Write(Path.Combine("src", "Widget.cs"));
        var groundTruth = fixture.Write("labels.json", """
        { "units": [ { "path": "src", "patterns": ["Wrapper"] } ] }
        """);

        Assert.Throws<InvalidDataException>(() => GroundTruth.Load(groundTruth, fixture.Root, Normalizer));
    }

    [Fact]
    public void A_missing_unit_path_fails_the_load()
    {
        using var fixture = new TempDirectory("dpd-groundtruth-tests-");
        var groundTruth = fixture.Write("labels.json", """
        { "units": [ { "path": "src/Gone", "patterns": ["Builder"] } ] }
        """);

        Assert.Throws<FileNotFoundException>(() => GroundTruth.Load(groundTruth, fixture.Root, Normalizer));
    }

    [Fact]
    public void A_unit_without_a_patterns_array_fails_the_load()
    {
        using var fixture = new TempDirectory("dpd-groundtruth-tests-");
        fixture.Write(Path.Combine("src", "Widget.cs"));
        var groundTruth = fixture.Write("labels.json", """
        { "units": [ { "path": "src" } ] }
        """);

        Assert.Throws<InvalidDataException>(() => GroundTruth.Load(groundTruth, fixture.Root, Normalizer));
    }
}
