using GitUpdater.DM;
using Microsoft.Extensions.Logging;

namespace GitUpdater.GitProviders;

public class GitLabGitProvider : GenericGitProvider, IGitProvider
{
    public GitLabGitProvider(ILogger<GitLabGitProvider> logger) : base(logger)
    {
    }

    public new RepoType RepoType => RepoType.GitLab;

    protected override string InjectToken(string repoUrl, string token)
    {
        if (string.IsNullOrEmpty(token))
            return repoUrl;

        var uri = new Uri(repoUrl);
        return $"{uri.Scheme}://oauth2:{token}@{uri.Host}{uri.PathAndQuery}";
    }
}
