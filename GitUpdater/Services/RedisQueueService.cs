using System.Text.Json;
using GitUpdater.DM;
using StackExchange.Redis;

namespace GitUpdater.Services;

public class RedisQueueService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisQueueService> _logger;
    private const string QueueKeyPrefix = "gitupdater:queue:";

    // Lua: append a QueueValue to the Values list, set Status to New if currently not InProgress
    private const string EnqueueScript = @"
        local data = redis.call('GET', KEYS[1])
        local obj
        if data then
            obj = cjson.decode(data)
        else
            obj = { Values = {}, Status = 0 }
        end
        local newItem = cjson.decode(ARGV[1])
        table.insert(obj.Values, newItem)
        if obj.Status ~= 1 then
            obj.Status = 0
        end
        redis.call('SET', KEYS[1], cjson.encode(obj))
        return 1";

    // Lua: if Status is not InProgress, set it to InProgress and return the full JSON; otherwise return nil
    private const string TryClaimScript = @"
        local data = redis.call('GET', KEYS[1])
        if not data then return nil end
        local obj = cjson.decode(data)
        if obj.Status == 1 then return nil end
        if #obj.Values == 0 then return nil end
        obj.Status = 1
        redis.call('SET', KEYS[1], cjson.encode(obj))
        return data";

    // Lua: set Status to Done and clear Values; delete the key
    private const string CompleteScript = @"
        redis.call('DEL', KEYS[1])
        return 1";

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

        await db.ScriptEvaluateAsync(EnqueueScript, new RedisKey[] { key }, new RedisValue[] { json });
        _logger.LogInformation("Enqueued request {RequestId} for repo {RepoUrl}", value.RequestId, repoUrl);
    }

    /// <summary>
    /// Atomically claims a repo queue for processing if its status is not InProgress.
    /// Returns the QueueValues snapshot to process, or null if already claimed by another instance.
    /// </summary>
    public async Task<QueueValues?> TryClaimAsync(string repoUrl)
    {
        var db = _redis.GetDatabase();
        var key = GetQueueKey(repoUrl);

        var result = await db.ScriptEvaluateAsync(TryClaimScript, new RedisKey[] { key });

        if (result.IsNull)
            return null;

        return JsonSerializer.Deserialize<QueueValues>(result.ToString()!);
    }

    /// <summary>
    /// Marks a repo queue as complete and removes it from Redis.
    /// </summary>
    public async Task CompleteAsync(string repoUrl)
    {
        var db = _redis.GetDatabase();
        var key = GetQueueKey(repoUrl);

        await db.ScriptEvaluateAsync(CompleteScript, new RedisKey[] { key });
        _logger.LogInformation("Completed and removed queue for repo {RepoUrl}", repoUrl);
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
