using DesignPatternDetection.Evaluation;

namespace DesignPatternDetection.Tests.Evaluation;

public class CorpusLoaderTests
{
    private readonly CorpusLoader _loader = new(new PatternNameNormalizer(
        ["Composite", "Decorator", "Strategy", "Factory Method", "Singleton"]));

    [Fact]
    public void Example_files_become_one_unit_each_and_unlabeled_files_are_skipped()
    {
        using var fixture = new TempDirectory("dpd-corpus-tests-");
        var singleton = fixture.Write("Singleton.cs");
        fixture.Write("Helpers.cs");

        var corpus = _loader.FromExampleFiles(fixture.Root);

        var unit = Assert.Single(corpus.Units);
        Assert.Equal("Singleton.cs", unit.Name);
        Assert.Equal([singleton], unit.Files);
        Assert.Equal(new HashSet<string> { "Singleton" }, unit.ExpectedPatterns);
        Assert.Equal(1, corpus.SkippedUnlabeled);
    }

    [Fact]
    public void A_pattern_named_directory_becomes_a_unit_with_everything_beneath_it()
    {
        using var fixture = new TempDirectory("dpd-corpus-tests-");
        var first = fixture.Write(Path.Combine("Composite.Conceptual", "Component.cs"));
        var second = fixture.Write(Path.Combine("Composite.Conceptual", "Nested", "Leaf.cs"));

        var corpus = _loader.FromLabeledFolders(fixture.Root);

        var unit = Assert.Single(corpus.Units);
        Assert.Equal("Composite.Conceptual", unit.Name);
        Assert.Equal([first, second], unit.Files);
        Assert.Equal(new HashSet<string> { "Composite" }, unit.ExpectedPatterns);
    }

    [Fact]
    public void A_dotted_project_prefix_still_labels_the_directory()
    {
        using var fixture = new TempDirectory("dpd-corpus-tests-");
        fixture.Write(Path.Combine("RefactoringGuru.DesignPatterns.FactoryMethod.Conceptual", "Program.cs"));

        var unit = Assert.Single(_loader.FromLabeledFolders(fixture.Root).Units);
        Assert.Equal(new HashSet<string> { "Factory Method" }, unit.ExpectedPatterns);
    }

    [Fact]
    public void A_matched_directory_is_not_searched_for_further_units()
    {
        using var fixture = new TempDirectory("dpd-corpus-tests-");
        fixture.Write(Path.Combine("Composite", "Decorator", "Wrapper.cs"));

        var unit = Assert.Single(_loader.FromLabeledFolders(fixture.Root).Units);
        Assert.Equal(new HashSet<string> { "Composite" }, unit.ExpectedPatterns);
    }

    [Fact]
    public void Build_output_and_dot_directories_are_never_entered()
    {
        using var fixture = new TempDirectory("dpd-corpus-tests-");
        var kept = fixture.Write(Path.Combine("Strategy", "Context.cs"));
        fixture.Write(Path.Combine("Strategy", "bin", "Debug", "Generated.cs"));
        fixture.Write(Path.Combine("Strategy", "obj", "Generated.cs"));
        fixture.Write(Path.Combine(".git", "Decorator", "Object.cs"));
        fixture.Write(Path.Combine("bin", "Composite", "Component.cs"));

        var corpus = _loader.FromLabeledFolders(fixture.Root);

        var unit = Assert.Single(corpus.Units);
        Assert.Equal([kept], unit.Files);
        Assert.Equal(0, corpus.SkippedUnlabeled);
    }

    [Fact]
    public void Unlabeled_source_directories_are_counted_as_skipped()
    {
        using var fixture = new TempDirectory("dpd-corpus-tests-");
        fixture.Write("Loose.cs");
        fixture.Write(Path.Combine("Utilities", "Helper.cs"));
        fixture.Write(Path.Combine("Strategy", "Context.cs"));

        var corpus = _loader.FromLabeledFolders(fixture.Root);

        Assert.Single(corpus.Units);
        Assert.Equal(2, corpus.SkippedUnlabeled);
    }
}
