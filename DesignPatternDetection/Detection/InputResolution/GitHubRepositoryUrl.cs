using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace DesignPatternDetection.Detection.InputResolution;

/// <summary>A GitHub repository named on the command line. Recognizes the forms a user is likely to paste.</summary>
public sealed partial record GitHubRepositoryUrl(string CloneUrl, string Owner, string Repository, string? Branch)
{
    [GeneratedRegex(@"^(?:https?://)?(?:www\.)?github\.com/(?<owner>[\w.-]+)/(?<repository>[\w.-]+?)(?:\.git)?(?:/tree/(?<branch>[\w./-]+?))?/?$", RegexOptions.IgnoreCase, "en-SI")]
    private static partial Regex GitHubUrlRegex();

    /// <summary>The <c>owner/repository</c> label, with the branch appended when one was named.</summary>
    public string Slug => Branch is null ? $"{Owner}/{Repository}" : $"{Owner}/{Repository}@{Branch}";

    public static bool TryParse(string argument, [NotNullWhen(true)] out GitHubRepositoryUrl? url)
    {
        var match = GitHubUrlRegex().Match(argument.Trim());
        if (!match.Success)
        {
            url = null;
            return false;
        }

        var owner = match.Groups["owner"].Value;
        var repository = match.Groups["repository"].Value;
        var branchMatch = match.Groups["branch"];
        var branch = branchMatch.Success ? branchMatch.Value : null;
        var cloneUrl = $"https://github.com/{owner}/{repository}.git";

        url = new GitHubRepositoryUrl(cloneUrl, owner, repository, branch);

        return true;
    }
}