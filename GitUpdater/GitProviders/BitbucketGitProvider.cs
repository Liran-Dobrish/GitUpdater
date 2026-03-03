using GitUpdater.DM;

namespace GitUpdater.GitProviders;

public class BitbucketGitProvider : GenericGitProvider, IGitProvider
{
    public BitbucketGitProvider(ILogger<BitbucketGitProvider> logger) : base(logger)
    {
    }

    public new RepoType RepoType => RepoType.Bitbucket;

    protected override string InjectToken(string repoUrl, string token)
    {
        if (string.IsNullOrEmpty(token))
            return repoUrl;

        var uri = new Uri(repoUrl);
        return $"{uri.Scheme}://x-token-auth:{token}@{uri.Host}{uri.PathAndQuery}";
    }
}
