using GitUpdater.DM;

namespace GitUpdater.GitProviders;

public class AzureReposGitProvider : GenericGitProvider, IGitProvider
{
    public AzureReposGitProvider(ILogger<AzureReposGitProvider> logger) : base(logger)
    {
    }

    public new RepoType RepoType => RepoType.AzureRepos;

    protected override string InjectToken(string repoUrl, string token)
    {
        if (string.IsNullOrEmpty(token))
            return repoUrl;

        var uri = new Uri(repoUrl);
        return $"{uri.Scheme}://pat:{token}@{uri.Host}{uri.PathAndQuery}";
    }
}
