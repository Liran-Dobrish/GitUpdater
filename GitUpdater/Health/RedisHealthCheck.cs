using GitUpdater.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GitUpdater.Health;

public class RedisHealthCheck : IHealthCheck
{
    private readonly RedisQueueService _queueService;

    public RedisHealthCheck(RedisQueueService queueService)
    {
        _queueService = queueService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var healthy = await _queueService.IsHealthyAsync();
        return healthy
            ? HealthCheckResult.Healthy("Redis is reachable")
            : HealthCheckResult.Unhealthy("Redis is not reachable");
    }
}
