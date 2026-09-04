using DesignPatternDetection.Detection.Patterns.Creational;

namespace DesignPatternDetection.Tests.Detection;

public class SparqlPatternDetectorTests
{
    private readonly SingletonPatternDetector _detector = new();

    [Fact]
    public void Detect_carries_the_qualified_fragment_for_each_role()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public sealed class Singleton
        {
            private Singleton() { }
            public static Singleton Instance { get; } = new Singleton();
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));

        // The label stays the stripped simple name; the fragment keeps the
        // qualified identity that joins to SourceGraph.Locations.
        Assert.Equal("Singleton", match.Bindings["class"]);
        Assert.NotNull(match.Fragments);
        Assert.Equal("Demo.Singleton", match.Fragments["class"]);
    }

    /// <summary>One result row is one match, in the order the query returned them.</summary>
    [Fact]
    public void Every_result_row_becomes_a_match()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public sealed class First
        {
            private First() { }
            public static First Instance { get; } = new First();
        }
        public sealed class Second
        {
            private Second() { }
            public static Second Instance { get; } = new Second();
        }
        """);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.Equal(["First", "Second"], matches.Select(match => match.Bindings["class"]).Order());
    }
}
