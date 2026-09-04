namespace DesignPatternDetection.Evaluation;

/// <summary>
/// One labeled unit of a corpus: the source files that are evaluated together in their own graph, and the patterns
/// those files are known to implement. An empty <paramref name="ExpectedPatterns"/> set is a deliberate negative
/// unit - every detection there is a false positive.
/// </summary>
public sealed record EvaluationUnit(
    string Name,
    IReadOnlyList<string> Files,
    IReadOnlySet<string> ExpectedPatterns);
