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

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var executionService = scope.ServiceProvider.GetRequiredService<JobExecutionService>();
            var recoveryService = scope.ServiceProvider.GetRequiredService<JobRecoveryService>();
            var claimService = scope.ServiceProvider.GetRequiredService<JobClaimService>();

            await recoveryService.RecoverStuckJobsAsync(stoppingToken);

            var job = await claimService.ClaimNextJobAsync(stoppingToken);

            if (job != null)
            {
                await executionService.ExecuteAsync(job, stoppingToken);
                await dbContext.SaveChangesAsync(stoppingToken);
            }

            await Task.Delay(_workerOptions.PollingIntervalSeconds * 1000, stoppingToken);
        }
    }
}
