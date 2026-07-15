using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Models;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Services;

public class JobWorker : BackgroundService
{
    private readonly ILogger<JobWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WorkerOptions _workerOptions;

    public JobWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<JobWorker> logger,
        IOptions<WorkerOptions> options
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _workerOptions = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobWorker started!");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var recoveryService = scope.ServiceProvider.GetRequiredService<JobRecoveryService>();
            var claimService = scope.ServiceProvider.GetRequiredService<JobClaimService>();
            var processor = scope.ServiceProvider.GetRequiredService<JobProcessor>();

            await recoveryService.RecoverStuckJobsAsync(stoppingToken);

            var job = await claimService.ClaimNextJobAsync(stoppingToken);

            if (job != null)
            {
                await processor.ProcessAsync(job, stoppingToken);
            }

            await Task.Delay(_workerOptions.PollingIntervalSeconds * 1000, stoppingToken);
        }
    }
}
