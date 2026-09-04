namespace DesignPatternDetection.Evaluation;

/// <summary>
/// Maps corpus labels to canonical detector pattern names. A candidate token matches a canonical name when both are
/// equal after stripping every non-letter character and lowercasing, which covers spellings like
/// <c>ChainOfResponsibility.cs</c>, <c>FactoryMethod.Conceptual</c> and
/// <c>RefactoringGuru.DesignPatterns.TemplateMethod.Conceptual</c>.
/// </summary>
public sealed class PatternNameNormalizer
{
    private readonly Dictionary<string, string> _canonicalByKey;

    public PatternNameNormalizer(IEnumerable<string> canonicalNames)
    {
        CanonicalNames = canonicalNames.Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
        _canonicalByKey = CanonicalNames.ToDictionary(Key, name => name);
    }

    public IReadOnlyList<string> CanonicalNames { get; }

    /// <summary>The canonical pattern name for a single label token, or null.</summary>
    public string? Normalize(string token)
    {
        var key = Key(token);
        return key.Length == 0 ? null : _canonicalByKey.GetValueOrDefault(key);
    }

    /// <summary>
    /// The canonical pattern name hidden in a dot-separated file or directory name (e.g.
    /// <c>AbstractFactory.Conceptual</c>), or null when no segment names a pattern.
    /// </summary>
    public string? NormalizeDottedName(string name) =>
        name.Split('.')
            .Select(Normalize)
            .FirstOrDefault(pattern => pattern is not null);

    private static string Key(string token) =>
        string.Concat(token.Where(char.IsLetter)).ToLowerInvariant();
}
