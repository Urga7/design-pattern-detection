namespace DesignPatternDetection.Evaluation;

/// <summary>
/// The labeled units discovered in a corpus, plus how many source-bearing locations carried no recognizable pattern
/// label and were therefore left out of the evaluation.
/// </summary>
public sealed record CorpusUnits(IReadOnlyList<EvaluationUnit> Units, int SkippedUnlabeled);

/// <summary>
/// Derives labeled units from names: in an examples corpus every <c>.cs</c> file whose name names a pattern is a
/// unit, and in a folder corpus every directory whose name (or dotted name segment) names a pattern is a unit -
/// which covers layouts like <c>AbstractFactory.Conceptual/</c> and
/// <c>RefactoringGuru.DesignPatterns.FactoryMethod.Conceptual/</c>. Corpora without pattern-named files or folders
/// need an explicit ground-truth file instead.
/// </summary>
public sealed class CorpusLoader(PatternNameNormalizer normalizer)
{
    /// <summary>One unit per pattern-named <c>.cs</c> file directly in the directory.</summary>
    public CorpusUnits FromExampleFiles(string directory)
    {
        var units = new List<EvaluationUnit>();
        var skipped = 0;

        foreach (var file in SortedSources(Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)))
        {
            var pattern = normalizer.NormalizeDottedName(Path.GetFileNameWithoutExtension(file));
            if (pattern is null)
            {
                skipped++;
                continue;
            }

            units.Add(new EvaluationUnit(Path.GetFileName(file), [file], new HashSet<string> { pattern }));
        }

        return new CorpusUnits(units, skipped);
    }

    /// <summary>
    /// One unit per pattern-named directory anywhere under the root. A matched directory is one unit including
    /// everything beneath it - its subdirectories are not searched for further units. Build output and
    /// dot-directories (<c>.git</c>) are never entered.
    /// </summary>
    public CorpusUnits FromLabeledFolders(string root)
    {
        var rootPath = Path.GetFullPath(root);
        var units = new List<EvaluationUnit>();
        var skipped = 0;

        Walk(rootPath);
        return new CorpusUnits(units, skipped);

        // Called only on unlabeled directories: any sources directly here belong to no unit.
        void Walk(string directory)
        {
            if (Directory.EnumerateFiles(directory, "*.cs").Any())
                skipped++;

            foreach (var child in Directory.EnumerateDirectories(directory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(child);
                if (IsNeverEntered(name))
                    continue;

                var pattern = normalizer.NormalizeDottedName(name);
                if (pattern is null)
                {
                    Walk(child);
                    continue;
                }

                var files = SortedSources(SourcesUnder(child));
                if (files.Count == 0)
                {
                    skipped++;
                    continue;
                }

                var unitName = Path.GetRelativePath(rootPath, child).Replace('\\', '/');
                units.Add(new EvaluationUnit(unitName, files, new HashSet<string> { pattern }));
            }
        }
    }

    private static bool IsNeverEntered(string directoryName) =>
        directoryName.StartsWith('.')
        || directoryName.Equals("bin", StringComparison.OrdinalIgnoreCase)
        || directoryName.Equals("obj", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SourcesUnder(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(directory, file)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(IsNeverEntered));

    private static List<string> SortedSources(IEnumerable<string> files) =>
        files
            .Select(Path.GetFullPath)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
