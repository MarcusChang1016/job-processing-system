using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Services;

public class JobWorker : BackgroundService
{
    private readonly ILogger<JobWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WorkerOptions _workerOptions;
    private readonly TimeProvider _timeProvider;

    public JobWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<JobWorker> logger,
        IOptions<WorkerOptions> options,
        TimeProvider timeProvider
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _workerOptions = options.Value;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startedAt = _timeProvider.GetUtcNow().UtcDateTime;

        _logger.LogInformation("JobWorker started at: {time}", startedAt);

        while (!stoppingToken.IsCancellationRequested)
        {
            var cycleNow = _timeProvider.GetUtcNow().UtcDateTime;

            using var scope = _serviceScopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var executionService = scope.ServiceProvider.GetRequiredService<JobExecutionService>();
            var recoveryService = scope.ServiceProvider.GetRequiredService<JobRecoveryService>();

            await recoveryService.RecoverStuckJobsAsync(stoppingToken);

            var job = await dbContext
                .Jobs.Where(job =>
                    job.Status == JobStatus.Pending
                    && (job.NextRetryAtUtc == null || job.NextRetryAtUtc <= cycleNow)
                    && (job.RetryCount < _workerOptions.MaxRetryCount)
                )
                .OrderBy(job => job.CreatedAtUtc)
                .FirstOrDefaultAsync(stoppingToken);

            if (job != null)
            {
                _logger.LogInformation("Processing job {id}", job.Id);

                if (!JobStateMachine.CanTransition(job.Status, JobStatus.Processing))
                {
                    _logger.LogWarning(
                        "Invalid transition {Current} -> {Next} for job {JobId}",
                        job.Status,
                        JobStatus.Processing,
                        job.Id
                    );

                    continue;
                }

                job.Status = JobStatus.Processing;
                job.ProcessingStartedAtUtc = cycleNow;
                job.UpdatedAtUtc = cycleNow;

                try
                {
                    dbContext.Jobs.Update(job);
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Job {JobId} was claimed by another worker", job.Id);
                    continue;
                }

                await executionService.ExecuteAsync(job, stoppingToken);
                await dbContext.SaveChangesAsync(stoppingToken);
            }

            await Task.Delay(_workerOptions.PollingIntervalSeconds * 1000, stoppingToken);
        }
    }
}
