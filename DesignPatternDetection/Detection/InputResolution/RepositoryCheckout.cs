using System.ComponentModel;
using System.Diagnostics;

namespace DesignPatternDetection.Detection.InputResolution;

/// <summary>A clone of a remote repository in a temporary directory, deleted when the checkout is disposed.</summary>
public sealed class RepositoryCheckout : IDisposable
{
    private RepositoryCheckout(string root) => Root = root;
    
    public string Root { get; }

    public static RepositoryCheckout Clone(GitHubRepositoryUrl url) => Clone(url.CloneUrl, url.Branch);

    public static RepositoryCheckout Clone(string cloneUrl, string? branch = null)
    {
        var root = Directory.CreateTempSubdirectory("dpd-clone-").FullName;

        try
        {
            RunClone(cloneUrl, branch, root);
        }
        catch
        {
            Delete(root);
            throw;
        }

        return new RepositoryCheckout(root);
    }

    public void Dispose() => Delete(Root);

    private static void RunClone(string cloneUrl, string? branch, string root)
    {
        var git = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            Environment = { ["GIT_TERMINAL_PROMPT"] = "0" }
        };
        
        git.ArgumentList.Add("-c");
        git.ArgumentList.Add("advice.detachedHead=false");

        git.ArgumentList.Add("clone");
        git.ArgumentList.Add("--depth");
        git.ArgumentList.Add("1");
        git.ArgumentList.Add("--single-branch");

        if (branch is not null)
        {
            git.ArgumentList.Add("--branch");
            git.ArgumentList.Add(branch);
        }

        git.ArgumentList.Add(cloneUrl);
        git.ArgumentList.Add(root);

        using var process = Start(git);
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Cloning '{cloneUrl}' failed: git exited with code {process.ExitCode}.");
    }

    private static Process Start(ProcessStartInfo git)
    {
        try
        {
            return Process.Start(git) ?? throw new InvalidOperationException("Could not start git.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "Scanning a repository URL requires the 'git' executable on PATH.", exception);
        }
    }

    /// <summary>
    /// Deletes a checkout directory, clearing read-only attributes first and retrying a few times.
    /// </summary>
    private static void Delete(string root)
    {
        if (!Directory.Exists(root))
            return;

        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                ClearReadOnlyAttributes(root);
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static void ClearReadOnlyAttributes(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
    }
}
