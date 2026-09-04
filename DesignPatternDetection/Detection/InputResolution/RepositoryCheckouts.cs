namespace DesignPatternDetection.Detection.InputResolution;

public sealed class RepositoryCheckouts
{
    private readonly List<RepositoryCheckout> _checkouts = [];

    /// <summary>Clones a repository into a temporary directory and tracks it for cleanup.</summary>
    public RepositoryCheckout Clone(GitHubRepositoryUrl url)
    {
        Console.WriteLine($"Cloning {url.Slug}...");

        var checkout = RepositoryCheckout.Clone(url);
        _checkouts.Add(checkout);

        return checkout;
    }

    /// <summary>Deletes every clone made so far.</summary>
    public void Cleanup()
    {
        foreach (var checkout in _checkouts)
            try
            {
                checkout.Dispose();
                Console.WriteLine($"Removed clone at {checkout.Root}.");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Could not remove the clone at {checkout.Root}: {exception.Message}");
            }

        _checkouts.Clear();
    }
}
