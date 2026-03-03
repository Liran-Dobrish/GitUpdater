using GitUpdater.DM;

namespace GitUpdater.GitProviders;

public class GitProviderFactory
{
    private readonly Dictionary<RepoType, IGitProvider> _providers;

    public GitProviderFactory(IEnumerable<IGitProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.RepoType);
    }

    public IGitProvider GetProvider(RepoType repoType)
    {
        if (_providers.TryGetValue(repoType, out var provider))
            return provider;

        return _providers[RepoType.Generic];
    }
}
