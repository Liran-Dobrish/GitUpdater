using GitUpdater.DM;

namespace GitUpdater.GitProviders;

public class GitHubGitProvider : GenericGitProvider, IGitProvider
{
    public GitHubGitProvider(ILogger<GitHubGitProvider> logger) : base(logger)
    {
    }

    public new RepoType RepoType => RepoType.GitHub;

    protected override string InjectToken(string repoUrl, string token)
    {
        if (string.IsNullOrEmpty(token))
            return repoUrl;

        var uri = new Uri(repoUrl);
        return $"{uri.Scheme}://x-access-token:{token}@{uri.Host}{uri.PathAndQuery}";
    }
}
