using System.Text.Json;
using GitUpdater.DM;
using GitUpdater.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace GitUpdater.Tests;

public class RedisQueueServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDb;
    private readonly RedisQueueService _service;

    public RedisQueueServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDb.Object);
        _service = new RedisQueueService(_mockRedis.Object, NullLogger<RedisQueueService>.Instance);
    }

    #region GetQueueKey Tests

    [Fact]
    public void GetQueueKey_ReturnsLowercasePrefixedKey()
    {
        var key = RedisQueueService.GetQueueKey("https://GitHub.com/Owner/Repo.git");
        Assert.Equal("gitupdater:queue:https://github.com/owner/repo.git", key);
    }

    [Fact]
    public void GetQueueKey_AlreadyLowercase_ReturnsSameKey()
    {
        var key = RedisQueueService.GetQueueKey("https://github.com/owner/repo.git");
        Assert.Equal("gitupdater:queue:https://github.com/owner/repo.git", key);
    }

    #endregion

    #region ExtractRepoUrl Tests

    [Fact]
    public void ExtractRepoUrl_ReturnsUrlWithoutPrefix()
    {
        var url = RedisQueueService.ExtractRepoUrl("gitupdater:queue:https://github.com/owner/repo.git");
        Assert.Equal("https://github.com/owner/repo.git", url);
    }

    #endregion

    #region EnqueueAsync Tests

    [Fact]
    public async Task EnqueueAsync_CallsScriptEvaluateWithCorrectKeyAndValue()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        var queueValue = new QueueValue
        {
            RequestId = Guid.NewGuid(),
            RepoUrl = repoUrl,
            Token = "token123",
            RepoType = RepoType.GitHub,
            Updates = new List<Update>
            {
                new Update { Type = UpdateType.File, Contents = "content", FileType = FileType.Text }
            }
        };

        var expectedKey = RedisQueueService.GetQueueKey(repoUrl);
        var expectedJson = JsonSerializer.Serialize(queueValue);

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1))
            .Verifiable();

        await _service.EnqueueAsync(repoUrl, queueValue);

        _mockDb.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == (RedisKey)expectedKey),
            It.Is<RedisValue[]>(vals => vals.Length == 1 && vals[0] == (RedisValue)expectedJson),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueAsync_MultipleValues_CallsScriptForEach()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        var value1 = new QueueValue { RequestId = Guid.NewGuid(), RepoUrl = repoUrl };
        var value2 = new QueueValue { RequestId = Guid.NewGuid(), RepoUrl = repoUrl };

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1));

        await _service.EnqueueAsync(repoUrl, value1);
        await _service.EnqueueAsync(repoUrl, value2);

        _mockDb.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EnqueueAsync_SerializesQueueValueToJson()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        var queueValue = new QueueValue
        {
            RequestId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            RepoUrl = repoUrl,
            Token = "mytoken",
            RepoType = RepoType.GitLab,
            Updates = new List<Update>()
        };

        string? capturedJson = null;
        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, keys, vals, flags) =>
            {
                capturedJson = vals[0];
            })
            .ReturnsAsync(RedisResult.Create((RedisValue)1));

        await _service.EnqueueAsync(repoUrl, queueValue);

        Assert.NotNull(capturedJson);
        var deserialized = JsonSerializer.Deserialize<QueueValue>(capturedJson!);
        Assert.NotNull(deserialized);
        Assert.Equal(queueValue.RequestId, deserialized!.RequestId);
        Assert.Equal(queueValue.Token, deserialized.Token);
        Assert.Equal(queueValue.RepoType, deserialized.RepoType);
    }

    #endregion

    #region TryClaimAsync Tests

    [Fact]
    public async Task TryClaimAsync_WhenRedisReturnsNull_ReturnsNull()
    {
        var repoUrl = "https://github.com/owner/repo.git";

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        var result = await _service.TryClaimAsync(repoUrl);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryClaimAsync_WhenRedisReturnsData_DeserializesQueueValues()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        var queueValues = new QueueValues
        {
            Status = QueueStatus.New,
            Values = new List<QueueValue>
            {
                new QueueValue
                {
                    RequestId = Guid.NewGuid(),
                    RepoUrl = repoUrl,
                    Token = "token",
                    RepoType = RepoType.GitHub
                }
            }
        };
        var json = JsonSerializer.Serialize(queueValues);

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)json));

        var result = await _service.TryClaimAsync(repoUrl);

        Assert.NotNull(result);
        Assert.Single(result!.Values);
        Assert.Equal(queueValues.Values[0].RequestId, result.Values[0].RequestId);
        Assert.Equal(QueueStatus.New, result.Status);
    }

    [Fact]
    public async Task TryClaimAsync_UsesCorrectKey()
    {
        var repoUrl = "https://github.com/Owner/Repo.git";
        var expectedKey = RedisQueueService.GetQueueKey(repoUrl);

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        await _service.TryClaimAsync(repoUrl);

        _mockDb.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == (RedisKey)expectedKey),
            It.Is<RedisValue[]>(vals => vals.Length == 2),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task TryClaimAsync_PassesEpochAndTimeoutArguments()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        RedisValue[]? capturedValues = null;

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, keys, vals, flags) =>
            {
                capturedValues = vals;
            })
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        var beforeEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _service.TryClaimAsync(repoUrl);
        var afterEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Assert.NotNull(capturedValues);
        Assert.Equal(2, capturedValues!.Length);

        var epoch = (long)capturedValues[0];
        Assert.InRange(epoch, beforeEpoch, afterEpoch);

        // StaleClaimTimeout is 10 minutes = 600 seconds
        var timeout = (long)capturedValues[1];
        Assert.Equal(600, timeout);
    }

    #endregion

    #region CompleteAsync Tests

    [Fact]
    public async Task CompleteAsync_CallsScriptEvaluateWithCorrectKey()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        var expectedKey = RedisQueueService.GetQueueKey(repoUrl);

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1));

        await _service.CompleteAsync(repoUrl);

        _mockDb.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == (RedisKey)expectedKey),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_NoAdditionalArguments()
    {
        var repoUrl = "https://github.com/owner/repo.git";

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1));

        await _service.CompleteAsync(repoUrl);

        // CompleteAsync should not pass any RedisValue arguments
        _mockDb.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys.Length == 1),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    #endregion

    #region Enqueue then Read (round-trip serialization) Tests

    [Fact]
    public async Task EnqueueAndClaim_RoundTripSerialization_PreservesAllFields()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        var requestId = Guid.NewGuid();
        var queueValue = new QueueValue
        {
            RequestId = requestId,
            RepoUrl = repoUrl,
            Token = "ghp_roundtrip",
            RepoType = RepoType.GitHub,
            Updates = new List<Update>
            {
                new Update { Type = UpdateType.File, Contents = "file content", FileType = FileType.Json },
                new Update { Type = UpdateType.Line, Contents = "line content", FileType = FileType.Yaml }
            },
            Done = false
        };

        // Capture the JSON that EnqueueAsync sends to Redis
        string? enqueuedJson = null;
        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, keys, vals, flags) =>
            {
                if (vals != null && vals.Length > 0)
                    enqueuedJson = vals[0];
            })
            .ReturnsAsync(RedisResult.Create((RedisValue)1));

        await _service.EnqueueAsync(repoUrl, queueValue);

        Assert.NotNull(enqueuedJson);

        // Simulate Redis returning a QueueValues containing the enqueued value
        var queueValues = new QueueValues
        {
            Status = QueueStatus.New,
            Values = new List<QueueValue> { JsonSerializer.Deserialize<QueueValue>(enqueuedJson!)! }
        };
        var claimJson = JsonSerializer.Serialize(queueValues);

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)claimJson));

        var claimed = await _service.TryClaimAsync(repoUrl);

        Assert.NotNull(claimed);
        Assert.Single(claimed!.Values);

        var claimedValue = claimed.Values[0];
        Assert.Equal(requestId, claimedValue.RequestId);
        Assert.Equal(repoUrl, claimedValue.RepoUrl);
        Assert.Equal("ghp_roundtrip", claimedValue.Token);
        Assert.Equal(RepoType.GitHub, claimedValue.RepoType);
        Assert.False(claimedValue.Done);
        Assert.Equal(2, claimedValue.Updates.Count);
        Assert.Equal(UpdateType.File, claimedValue.Updates[0].Type);
        Assert.Equal("file content", claimedValue.Updates[0].Contents);
        Assert.Equal(FileType.Json, claimedValue.Updates[0].FileType);
        Assert.Equal(UpdateType.Line, claimedValue.Updates[1].Type);
        Assert.Equal("line content", claimedValue.Updates[1].Contents);
        Assert.Equal(FileType.Yaml, claimedValue.Updates[1].FileType);
    }

    [Fact]
    public async Task TryClaimAsync_WithMultipleQueuedValues_DeserializesAll()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        var queueValues = new QueueValues
        {
            Status = QueueStatus.New,
            Values = new List<QueueValue>
            {
                new QueueValue { RequestId = Guid.NewGuid(), RepoUrl = repoUrl, Token = "t1" },
                new QueueValue { RequestId = Guid.NewGuid(), RepoUrl = repoUrl, Token = "t2" },
                new QueueValue { RequestId = Guid.NewGuid(), RepoUrl = repoUrl, Token = "t3" }
            }
        };
        var json = JsonSerializer.Serialize(queueValues);

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)json));

        var result = await _service.TryClaimAsync(repoUrl);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Values.Count);
        Assert.Equal("t1", result.Values[0].Token);
        Assert.Equal("t2", result.Values[1].Token);
        Assert.Equal("t3", result.Values[2].Token);
    }

    [Fact]
    public async Task TryClaimAsync_WithInProgressStatus_DeserializesStatus()
    {
        var repoUrl = "https://github.com/owner/repo.git";
        var queueValues = new QueueValues
        {
            Status = QueueStatus.InProgress,
            Values = new List<QueueValue>
            {
                new QueueValue { RequestId = Guid.NewGuid(), RepoUrl = repoUrl }
            }
        };
        var json = JsonSerializer.Serialize(queueValues);

        _mockDb
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)json));

        var result = await _service.TryClaimAsync(repoUrl);

        Assert.NotNull(result);
        Assert.Equal(QueueStatus.InProgress, result!.Status);
    }

    #endregion
}
