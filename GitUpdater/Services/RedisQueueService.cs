using System.Text.Json;
using GitUpdater.DM;
using StackExchange.Redis;

namespace GitUpdater.Services;

public class RedisQueueService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisQueueService> _logger;
    private const string QueueKeyPrefix = "gitupdater:queue:";

    public RedisQueueService(IConnectionMultiplexer redis, ILogger<RedisQueueService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public static string GetQueueKey(string repoUrl) => $"{QueueKeyPrefix}{repoUrl.ToLowerInvariant()}";

    public async Task EnqueueAsync(string repoUrl, QueueValue value)
    {
        var db = _redis.GetDatabase();
        var key = GetQueueKey(repoUrl);
        var json = JsonSerializer.Serialize(value);

        await db.ListRightPushAsync(key, json);
        _logger.LogInformation("Enqueued request {RequestId} for repo {RepoUrl}", value.RequestId, repoUrl);
    }

    public async Task<QueueValue?> DequeueAsync(string repoUrl)
    {
        var db = _redis.GetDatabase();
        var key = GetQueueKey(repoUrl);
        var value = await db.ListLeftPopAsync(key);

        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<QueueValue>(value!);
    }

    public async Task<long> GetQueueLengthAsync(string repoUrl)
    {
        var db = _redis.GetDatabase();
        var key = GetQueueKey(repoUrl);
        return await db.ListLengthAsync(key);
    }

    public async Task DeleteQueueAsync(string repoUrl)
    {
        var db = _redis.GetDatabase();
        var key = GetQueueKey(repoUrl);
        await db.KeyDeleteAsync(key);
        _logger.LogInformation("Deleted queue for repo {RepoUrl}", repoUrl);
    }

    public async Task<List<string>> GetAllQueueKeysAsync()
    {
        var server = _redis.GetServers().First();
        var keys = new List<string>();

        await foreach (var key in server.KeysAsync(pattern: $"{QueueKeyPrefix}*"))
        {
            keys.Add(key.ToString());
        }

        return keys;
    }

    public static string ExtractRepoUrl(string queueKey) =>
        queueKey.Substring(QueueKeyPrefix.Length);
}
