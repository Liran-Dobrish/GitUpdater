using System.Collections.Concurrent;
using System.Diagnostics;
using GitUpdater.GitProviders;

namespace GitUpdater.Services;

public class QueueProcessorService : BackgroundService
{
    private readonly RedisQueueService _queueService;
    private readonly GitProviderFactory _gitProviderFactory;
    private readonly ILogger<QueueProcessorService> _logger;
    private readonly ConcurrentDictionary<string, Task> _activeProcessors = new();
    private static readonly ActivitySource ActivitySource = new("GitUpdater.QueueProcessor");

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public QueueProcessorService(
        RedisQueueService queueService,
        GitProviderFactory gitProviderFactory,
        ILogger<QueueProcessorService> logger)
    {
        _queueService = queueService;
        _gitProviderFactory = gitProviderFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue processor service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var queueKeys = await _queueService.GetAllQueueKeysAsync();

                foreach (var key in queueKeys)
                {
                    var repoUrl = RedisQueueService.ExtractRepoUrl(key);

                    _activeProcessors.AddOrUpdate(
                        repoUrl,
                        url => Task.Run(() => ProcessQueueAsync(url, stoppingToken), stoppingToken),
                        (url, existingTask) =>
                        {
                            if (existingTask.IsCompleted)
                                return Task.Run(() => ProcessQueueAsync(url, stoppingToken), stoppingToken);
                            return existingTask;
                        });
                }

                // Clean up completed processors
                var completedKeys = _activeProcessors
                    .Where(kvp => kvp.Value.IsCompleted)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in completedKeys)
                {
                    _activeProcessors.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during queue polling");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("Queue processor service stopping, waiting for active processors...");
        await Task.WhenAll(_activeProcessors.Values);
    }

    private async Task ProcessQueueAsync(string repoUrl, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("ProcessQueue", ActivityKind.Consumer);
        activity?.SetTag("repo.url", repoUrl);

        _logger.LogInformation("Starting queue processor for {RepoUrl}", repoUrl);

        string? localPath = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var queueValue = await _queueService.DequeueAsync(repoUrl);

                if (queueValue == null)
                {
                    _logger.LogInformation("Queue empty for {RepoUrl}, cleaning up", repoUrl);
                    await _queueService.DeleteQueueAsync(repoUrl);
                    break;
                }

                using var itemActivity = ActivitySource.StartActivity("ProcessQueueItem", ActivityKind.Internal);
                itemActivity?.SetTag("request.id", queueValue.RequestId.ToString());
                itemActivity?.SetTag("repo.type", queueValue.RepoType.ToString());

                try
                {
                    var provider = _gitProviderFactory.GetProvider(queueValue.RepoType);

                    // Clone on first item if not already cloned
                    if (localPath == null)
                    {
                        localPath = Path.Combine(Path.GetTempPath(), "gitupdater", Guid.NewGuid().ToString());
                        Directory.CreateDirectory(localPath);

                        using var cloneActivity = ActivitySource.StartActivity("GitClone");
                        await provider.CloneAsync(queueValue.RepoUrl, localPath, queueValue.Token, cancellationToken);
                    }
                    else
                    {
                        using var pullActivity = ActivitySource.StartActivity("GitPull");
                        await provider.PullAsync(localPath, queueValue.Token, cancellationToken);
                    }

                    // Apply updates
                    var modifiedFiles = await ApplyUpdatesAsync(localPath, queueValue, cancellationToken);

                    if (modifiedFiles.Count > 0)
                    {
                        using var addActivity = ActivitySource.StartActivity("GitAdd");
                        await provider.AddAsync(localPath, modifiedFiles, cancellationToken);

                        using var commitActivity = ActivitySource.StartActivity("GitCommit");
                        await provider.CommitAsync(localPath, $"GitUpdater: Apply updates for request {queueValue.RequestId}", cancellationToken);

                        using var pushActivity = ActivitySource.StartActivity("GitPush");
                        await provider.PushAsync(localPath, queueValue.Token, cancellationToken);
                    }

                    _logger.LogInformation("Processed request {RequestId} for {RepoUrl}", queueValue.RequestId, repoUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request {RequestId} for {RepoUrl}", queueValue.RequestId, repoUrl);
                    itemActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                }
            }
        }
        finally
        {
            // Cleanup local clone
            if (localPath != null && Directory.Exists(localPath))
            {
                try
                {
                    Directory.Delete(localPath, recursive: true);
                    _logger.LogInformation("Cleaned up local path {LocalPath}", localPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up local path {LocalPath}", localPath);
                }
            }
        }
    }

    private static async Task<List<string>> ApplyUpdatesAsync(string localPath, DM.QueueValue queueValue, CancellationToken cancellationToken)
    {
        var modifiedFiles = new List<string>();

        foreach (var update in queueValue.Updates)
        {
            if (string.IsNullOrEmpty(update.Contents))
                continue;

            switch (update.Type)
            {
                case DM.UpdateType.File:
                    // Contents format: "relativePath::fileContents"
                    var fileSeparatorIndex = update.Contents.IndexOf("::", StringComparison.Ordinal);
                    if (fileSeparatorIndex > 0)
                    {
                        var relativePath = update.Contents[..fileSeparatorIndex];
                        var fileContents = update.Contents[(fileSeparatorIndex + 2)..];
                        var fullPath = Path.Combine(localPath, relativePath);

                        var dir = Path.GetDirectoryName(fullPath);
                        if (dir != null)
                            Directory.CreateDirectory(dir);

                        await File.WriteAllTextAsync(fullPath, fileContents, cancellationToken);
                        modifiedFiles.Add(relativePath);
                    }
                    break;

                case DM.UpdateType.Line:
                    // Contents format: "relativePath::lineNumber::newLineContent"
                    var parts = update.Contents.Split("::", 3);
                    if (parts.Length == 3 && int.TryParse(parts[1], out var lineNumber))
                    {
                        var filePath = Path.Combine(localPath, parts[0]);
                        if (File.Exists(filePath))
                        {
                            var lines = (await File.ReadAllLinesAsync(filePath, cancellationToken)).ToList();
                            if (lineNumber >= 0 && lineNumber < lines.Count)
                                lines[lineNumber] = parts[2];
                            else
                                lines.Add(parts[2]);

                            await File.WriteAllLinesAsync(filePath, lines, cancellationToken);
                            modifiedFiles.Add(parts[0]);
                        }
                    }
                    break;
            }
        }

        return modifiedFiles;
    }
}
