using GitUpdater.DM;
using GitUpdater.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitUpdater.Controllers;

[ApiController]
[Route("[controller]")]
public class GitUpdateController : ControllerBase
{
    private readonly ILogger<GitUpdateController> _logger;
    private readonly RedisQueueService _queueService;

    public GitUpdateController(ILogger<GitUpdateController> logger, RedisQueueService queueService)
    {
        _logger = logger;
        _queueService = queueService;
    }

    [HttpPost()]
    public async Task<Guid> AddRequest([FromBody] GitUpdateRequest request)
    {
        Guid requestId = Guid.NewGuid();

        var queueValue = new QueueValue
        {
            RequestId = requestId,
            RepoUrl = request.RepoUrl,
            Token = request.Token,
            RepoType = request.Type,
            Updates = request.Updates
        };

        await _queueService.EnqueueAsync(request.RepoUrl, queueValue);

        _logger.LogInformation("Request {RequestId} enqueued for repo {RepoUrl}", requestId, request.RepoUrl);

        return requestId;
    }
}
