using DesignPatternDetection.Detection.InputResolution;

namespace DesignPatternDetection.Tests.Detection.InputResolution;

public class GitHubRepositoryUrlTests
{
    [Theory]
    [InlineData("https://github.com/dotnet/runtime")]
    [InlineData("https://github.com/dotnet/runtime/")]
    [InlineData("https://github.com/dotnet/runtime.git")]
    [InlineData("http://www.github.com/dotnet/runtime")]
    [InlineData("github.com/dotnet/runtime")]
    [InlineData("  https://github.com/dotnet/runtime  ")]
    public void Normalizes_the_pasted_forms_of_a_repository_url(string argument)
    {
        Assert.True(GitHubRepositoryUrl.TryParse(argument, out var url));
        Assert.Equal("https://github.com/dotnet/runtime.git", url.CloneUrl);
        Assert.Equal("dotnet", url.Owner);
        Assert.Equal("runtime", url.Repository);
        Assert.Null(url.Branch);
        Assert.Equal("dotnet/runtime", url.Slug);
    }

    [Theory]
    [InlineData("https://github.com/dotnet/runtime/tree/release", "release")]
    [InlineData("https://github.com/dotnet/runtime/tree/release/9.0/", "release/9.0")]
    public void Reads_the_branch_from_a_tree_url(string argument, string branch)
    {
        Assert.True(GitHubRepositoryUrl.TryParse(argument, out var url));
        Assert.Equal("https://github.com/dotnet/runtime.git", url.CloneUrl);
        Assert.Equal(branch, url.Branch);
        Assert.Equal("dotnet/runtime@" + branch, url.Slug);
    }

    [Theory]
    [InlineData(@"C:\GitHub\design-pattern-detection")]
    [InlineData("Detection/SourceFileResolver.cs")]
    [InlineData("App.slnx")]
    [InlineData("https://gitlab.com/dotnet/runtime")]
    [InlineData("https://github.com/dotnet")]
    [InlineData("https://not-github.com/dotnet/runtime")]
    public void Leaves_anything_that_is_not_a_github_repository_to_the_path_resolver(string argument)
    {
        Assert.False(GitHubRepositoryUrl.TryParse(argument, out var url));
        Assert.Null(url);
    }
}
