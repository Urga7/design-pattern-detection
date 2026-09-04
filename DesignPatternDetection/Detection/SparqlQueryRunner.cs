using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Writing;

namespace DesignPatternDetection.Detection;

/// <summary>
/// Runs a user-supplied SPARQL query against a source graph and writes the result: SELECT rows render as
/// "variable = value" lines plus a row count, ASK as true/false, CONSTRUCT/DESCRIBE as Turtle. URI nodes render as
/// their qualified fragment (<c>Demo.Notifier</c>).
/// </summary>
public static class SparqlQueryRunner
{
    public static void Run(TextWriter output, IGraph graph, string sparql)
    {
        switch (graph.ExecuteQuery(sparql))
        {
            case SparqlResultSet { ResultsType: SparqlResultsType.Boolean } ask:
                output.WriteLine(ask.Result ? "true" : "false");
                break;

            case SparqlResultSet rows:
                foreach (var row in rows)
                    output.WriteLine(string.Join(", ", Cells(row)));
                output.WriteLine($"{rows.Count} row(s).");
                break;

            case IGraph constructed:
                new CompressingTurtleWriter().Save(constructed, output, leaveOpen: true);
                break;
        }
    }

    private static IEnumerable<string> Cells(ISparqlResult row)
    {
        foreach (var variable in row.Variables)
        {
            if (row.TryGetBoundValue(variable, out var node))
                yield return $"{variable} = {Render(node)}";
        }
    }

    private static string Render(INode node) => node switch
    {
        IUriNode uriNode when !string.IsNullOrEmpty(uriNode.Uri.Fragment) => uriNode.Uri.Fragment.TrimStart('#'),
        ILiteralNode literal => literal.Value,
        _ => node.ToString() ?? string.Empty
    };
}
