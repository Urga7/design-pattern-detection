using System.Diagnostics;
using DesignPatternDetection.Detection.InputResolution;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// Where one corpus was found: the name its report carries, the directory to scan, and - for a clone - the commit it
/// sits on.
/// </summary>
public sealed record ResolvedCorpus(string Name, string Root, string? Commit);

/// <summary>
/// Resolves a corpus argument - a directory, a GitHub repository URL, or one of the built-in names <c>examples</c>
/// and <c>refactoring-guru</c> - to the sources to evaluate, cloning what has to be cloned and tracking the clones
/// for <see cref="Cleanup"/>.
/// </summary>
public sealed class CorpusResolver
{
    private const string RefactoringGuruUrl = "https://github.com/RefactoringGuru/design-patterns-csharp";

    private readonly RepositoryCheckouts _checkouts = new();

    public ResolvedCorpus Resolve(string? argument)
    {
        if (IsBundledExamples(argument))
            return new ResolvedCorpus("examples", ExamplesDirectory.Locate(), null);

        if (argument!.Equals("refactoring-guru", StringComparison.OrdinalIgnoreCase))
            argument = RefactoringGuruUrl;

        if (GitHubRepositoryUrl.TryParse(argument, out var url))
        {
            var checkout = _checkouts.Clone(url);

            return new ResolvedCorpus(url.Slug, checkout.Root, ReadHeadCommit(checkout.Root));
        }

        var fullPath = Path.GetFullPath(argument);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"'{argument}' is not a directory, GitHub URL or known corpus name.");

        return new ResolvedCorpus(argument, fullPath, null);
    }

    /// <summary>Deletes every clone made so far.</summary>
    public void Cleanup() => _checkouts.Cleanup();

    public static bool IsBundledExamples(string? argument) =>
        argument is null || argument.Equals("examples", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The commit a clone sits on. Best effort: a corpus that is not a git checkout, or a machine without git,
    /// reports no commit rather than failing the run.
    /// </summary>
    private static string? ReadHeadCommit(string root)
    {
        try
        {
            var git = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            git.ArgumentList.Add("-C");
            git.ArgumentList.Add(root);
            git.ArgumentList.Add("rev-parse");
            git.ArgumentList.Add("HEAD");

            using var process = Process.Start(git);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
