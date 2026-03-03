using System.Diagnostics;
using GitUpdater.DM;

namespace GitUpdater.GitProviders;

public class GenericGitProvider : IGitProvider
{
    private readonly ILogger<GenericGitProvider> _logger;

    public GenericGitProvider(ILogger<GenericGitProvider> logger)
    {
        _logger = logger;
    }

    public RepoType RepoType => RepoType.Generic;

    public async Task CloneAsync(string repoUrl, string localPath, string token, CancellationToken cancellationToken = default)
    {
        var authenticatedUrl = InjectToken(repoUrl, token);
        await RunGitCommandAsync($"clone {authenticatedUrl} {localPath}", workingDirectory: null, cancellationToken);
    }

    public async Task AddAsync(string localPath, IEnumerable<string> files, CancellationToken cancellationToken = default)
    {
        foreach (var file in files)
        {
            await RunGitCommandAsync($"add {file}", localPath, cancellationToken);
        }
    }

    public async Task CommitAsync(string localPath, string message, CancellationToken cancellationToken = default)
    {
        await RunGitCommandAsync($"commit -m \"{message}\"", localPath, cancellationToken);
    }

    public async Task PushAsync(string localPath, string token, CancellationToken cancellationToken = default)
    {
        await RunGitCommandAsync("push", localPath, cancellationToken);
    }

    public async Task PullAsync(string localPath, string token, CancellationToken cancellationToken = default)
    {
        await RunGitCommandAsync("pull", localPath, cancellationToken);
    }

    protected virtual string InjectToken(string repoUrl, string token)
    {
        if (string.IsNullOrEmpty(token))
            return repoUrl;

        var uri = new Uri(repoUrl);
        return $"{uri.Scheme}://oauth2:{token}@{uri.Host}{uri.PathAndQuery}";
    }

    protected async Task RunGitCommandAsync(string arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running git {Arguments} in {WorkingDirectory}", arguments, workingDirectory ?? ".");

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(workingDirectory))
            psi.WorkingDirectory = workingDirectory;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogError("Git command failed: {Error}", error);
            throw new InvalidOperationException($"Git command 'git {arguments}' failed with exit code {process.ExitCode}: {error}");
        }

        if (!string.IsNullOrWhiteSpace(output))
            _logger.LogDebug("Git output: {Output}", output);
    }
}
