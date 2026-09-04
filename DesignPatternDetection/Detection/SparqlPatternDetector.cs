using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Parsing;

namespace DesignPatternDetection.Detection;

/// <summary>
/// Base class for detectors that express their detection rule as a SPARQL query over the source-code graph produced
/// by <see cref="SourceGraphBuilder"/>. Every selected variable is a role, and one result row becomes one match.
/// </summary>
public abstract class SparqlPatternDetector : IPatternDetector
{
    /// <summary>How long any one detector may spend on its query before it is abandoned.</summary>
    public static TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(50);

    private static readonly SparqlQueryParser QueryParser = new();

    public abstract string PatternName { get; }

    /// <summary>The SPARQL <c>SELECT</c> query that describes the pattern.</summary>
    protected abstract string SparqlQuery { get; }

    /// <summary>
    /// Maps this detector's role variables onto FDP's names for the same pattern's participants (role variable -&gt;
    /// FDP local name, e.g. <c>"adaptee"</c> -&gt; <c>"AdapterService"</c>). Partial: a role FDP names no participant
    /// for is left out and reported without an IRI, and an empty table means FDP does not model the pattern at all.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>();

    /// <summary>
    /// The local name of the FDP individual for the pattern itself (<c>Adapter</c>), which FDP spells as the display
    /// name with the spaces taken out. Override where that is not the ontology's spelling.
    /// </summary>
    protected virtual string FdpPattern => PatternName.Replace(" ", "");

    public IReadOnlyList<PatternMatch> Detect(IGraph graph)
    {
        var results = (SparqlResultSet)Execute(graph);
        var variables = results.Variables.ToList();
        var matches = new List<PatternMatch>();

        foreach (var result in results)
        {
            var bindings = new Dictionary<string, string>();
            var fragments = new Dictionary<string, string>();

            foreach (var variable in variables)
            {
                if (!result.TryGetBoundValue(variable, out var node))
                    continue;

                bindings[variable] = ToLabel(node);
                if (ToFragment(node) is { } fragment)
                    fragments[variable] = fragment;
            }

            matches.Add(new PatternMatch(
                PatternName,
                bindings,
                fragments,
                RoleIris: RoleIrisFor(bindings.Keys),
                PatternIri: PatternIri));
        }

        return matches;
    }

    /// <summary>Runs the query under <see cref="QueryTimeout"/>.</summary>
    private object Execute(IGraph graph)
    {
        var query = QueryParser.ParseFromString(SparqlQuery);
        var libraryLimit = QueryTimeout - TimeSpan.FromSeconds(1);

        var processor = new LeviathanQueryProcessor(
            new InMemoryDataset(graph),
            options => options.QueryExecutionTimeout = (long)libraryLimit.TotalMilliseconds);

        var execution = Task.Run(() => processor.ProcessQuery(query));

        try
        {
            if (!execution.Wait(QueryTimeout))
                throw new RdfQueryTimeoutException(Timeout);

            return execution.Result;
        }
        catch (AggregateException exception) when (exception.InnerException is RdfQueryTimeoutException)
        {
            throw new RdfQueryTimeoutException(Timeout);
        }
    }

    private static string Timeout => $"the query exceeded the {QueryTimeout.TotalSeconds:0}s limit on this graph";

    /// <summary>The IRI of the FDP individual naming this pattern, or null when <see cref="FdpRoles"/> is empty.</summary>
    private string? PatternIri => FdpRoles.Count > 0 ? FdpVocabulary.Pattern(FdpPattern) : null;

    /// <summary>The FDP IRIs for the roles this match bound, or null when the detector aligns none of them.</summary>
    private Dictionary<string, string>? RoleIrisFor(IEnumerable<string> boundRoles)
    {
        var aligned = FdpRoles;
        if (aligned.Count == 0)
            return null;

        var iris = boundRoles
            .Where(aligned.ContainsKey)
            .ToDictionary(role => role, role => FdpVocabulary.Role(aligned[role]));

        return iris.Count > 0 ? iris : null;
    }

    /// <summary>
    /// Renders a node as a short label: the URI fragment with any dotted namespace qualification stripped
    /// (e.g. "Demo.Singleton" -&gt; "Singleton").
    /// </summary>
    private static string ToLabel(INode node)
    {
        if (node is not IUriNode uriNode || string.IsNullOrEmpty(uriNode.Uri.Fragment))
            return node.ToString();

        var fragment = uriNode.Uri.Fragment.TrimStart('#');
        var lastDot = fragment.LastIndexOf('.');
        return lastDot >= 0 ? fragment[(lastDot + 1)..] : fragment;
    }

    /// <summary>
    /// The node's full URI fragment (e.g. "Demo.Singleton") - the key into <see cref="SourceGraph.Locations"/>; null
    /// for literals and blank nodes.
    /// </summary>
    private static string? ToFragment(INode node) =>
        node is IUriNode uriNode && !string.IsNullOrEmpty(uriNode.Uri.Fragment)
            ? uriNode.Uri.Fragment.TrimStart('#')
            : null;
}
