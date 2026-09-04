using DesignPatternDetection.Detection;
using VDS.RDF;

namespace DesignPatternDetection.Tests.Detection;

public class SparqlQueryRunnerTests
{
    private const string Prefixes = """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        """;

    [Fact]
    public void Select_renders_qualified_fragments_and_a_row_count()
    {
        var graph = TestGraph.From("namespace Demo; public class C { }");

        var output = Run(graph, $"{Prefixes}\nSELECT ?c WHERE {{ ?c rdf:type src:Class }}");

        Assert.Contains("c = Demo.C", output);
        Assert.Contains("1 row(s).", output);
    }

    [Fact]
    public void An_empty_select_prints_zero_rows()
    {
        var graph = TestGraph.From("namespace Demo; public class C { }");

        var output = Run(graph, $"{Prefixes}\nSELECT ?i WHERE {{ ?i rdf:type src:Interface }}");

        Assert.Equal("0 row(s).", output.Trim());
    }

    [Fact]
    public void Ask_prints_a_boolean()
    {
        var graph = TestGraph.From("namespace Demo; public class C { }");

        var output = Run(graph, $"{Prefixes}\nASK {{ ?c rdf:type src:Class }}");

        Assert.Equal("true", output.Trim());
    }

    [Fact]
    public void Construct_emits_turtle()
    {
        var graph = TestGraph.From("namespace Demo; public class C { }");

        var output = Run(
            graph,
            $"{Prefixes}\nCONSTRUCT {{ ?c rdf:type src:Class }} WHERE {{ ?c rdf:type src:Class }}");

        Assert.Contains("Demo.C", output);
    }

    private static string Run(IGraph graph, string sparql)
    {
        var writer = new StringWriter();
        SparqlQueryRunner.Run(writer, graph, sparql);
        return writer.ToString();
    }
}
