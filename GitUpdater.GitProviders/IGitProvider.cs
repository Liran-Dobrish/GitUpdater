using Microsoft.Extensions.Logging;

namespace GitUpdater.GitProviders;

public interface IGitProvider
{
    DM.RepoType RepoType { get; }
    Task CloneAsync(string repoUrl, string localPath, string token, CancellationToken cancellationToken = default);
    Task AddAsync(string localPath, IEnumerable<string> files, CancellationToken cancellationToken = default);
    Task CommitAsync(string localPath, string message, CancellationToken cancellationToken = default);
    Task PushAsync(string localPath, string token, CancellationToken cancellationToken = default);
    Task PullAsync(string localPath, string token, CancellationToken cancellationToken = default);
}
