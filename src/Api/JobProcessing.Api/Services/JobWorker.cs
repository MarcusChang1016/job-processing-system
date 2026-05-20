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
        _logger.LogInformation("JobWorker started at: {time}", DateTime.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var executionService = scope.ServiceProvider.GetRequiredService<JobExecutionService>();

            var timeoutThreshold = DateTime.UtcNow.AddSeconds(
                -_workerOptions.StuckJobTimeoutSeconds
            );

            var stuckJobs = await dbContext
                .Jobs.Where(job =>
                    job.Status == JobStatus.Processing
                    && job.ProcessingStartedAtUtc != null
                    && job.ProcessingStartedAtUtc < timeoutThreshold
                )
                .ToListAsync(stoppingToken);

            foreach (var stuckJob in stuckJobs)
            {
                _logger.LogWarning("Recovering stuck job {id}", stuckJob.Id);

                stuckJob.RetryCount += 1;
                stuckJob.UpdatedAtUtc = DateTime.UtcNow;
                stuckJob.ProcessingStartedAtUtc = null;
                stuckJob.LastErrorMessage = "Recovered from stale processing state";

                if (stuckJob.RetryCount < _workerOptions.MaxRetryCount)
                {
                    stuckJob.Status = JobStatus.Pending;
                }
                else
                {
                    stuckJob.Status = JobStatus.Failed;
                }
            }
            await dbContext.SaveChangesAsync(stoppingToken);

            var job = await dbContext
                .Jobs.Where(job =>
                    job.Status == JobStatus.Pending
                    && (job.NextRetryAtUtc == null || job.NextRetryAtUtc <= DateTime.UtcNow)
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
                job.ProcessingStartedAtUtc = DateTime.UtcNow;
                job.UpdatedAtUtc = DateTime.UtcNow;

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
