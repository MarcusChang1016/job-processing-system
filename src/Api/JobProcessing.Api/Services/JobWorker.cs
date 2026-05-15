using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JobProcessing.Api.Services;

public class JobWorker : BackgroundService
{
    private readonly ILogger<JobWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Random _random = new();

    public JobWorker(IServiceScopeFactory serviceScopeFactory, ILogger<JobWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobWorker started at: {time}", DateTime.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var timeoutThreshold = DateTime.UtcNow.AddSeconds(-30);

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

                stuckJob.Status = JobStatus.Pending;
                stuckJob.UpdatedAtUtc = DateTime.UtcNow;
                stuckJob.ProcessingStartedAtUtc = null;
                stuckJob.LastErrorMessage = "Recovered from stale processing state";
            }
            await dbContext.SaveChangesAsync(stoppingToken);

            var job = await dbContext
                .Jobs.Where(job =>
                    job.Status == JobStatus.Pending
                    && (job.NextRetryAtUtc == null || job.NextRetryAtUtc <= DateTime.UtcNow)
                    && (job.RetryCount < 3)
                )
                .OrderBy(job => job.CreatedAtUtc)
                .FirstOrDefaultAsync(stoppingToken);

            if (job != null)
            {
                if (job.CompletedAtUtc != null)
                {
                    _logger.LogWarning("Skipping already completed job {JobId}", job.Id);
                    continue;
                }

                var startAt = DateTime.UtcNow;

                try
                {
                    _logger.LogInformation("Processing job {id}", job.Id);

                    // Simulate job processing
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

                    // Simulate processing time
                    await Task.Delay(2000, stoppingToken);

                    // Simulate random failure / success
                    bool isFailed = _random.Next(0, 2) == 0; // 50% chance of failure

                    if (isFailed)
                        throw new Exception("Simulated failure");

                    job.Status = JobStatus.Success;
                    job.UpdatedAtUtc = DateTime.UtcNow;
                    job.CompletedAtUtc = DateTime.UtcNow;
                    job.LastErrorMessage = null;
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Job {id} completed successfully", job.Id);

                    var result = new JobResult
                    {
                        JobId = job.Id,
                        Success = true,
                        RetryCount = job.RetryCount,
                        StartedAtUtc = startAt,
                        FinishedAtUtc = DateTime.UtcNow,
                    };

                    _logger.LogInformation("JobResult {@JobResult}", result);
                }
                catch (Exception ex)
                {
                    job.RetryCount += 1;
                    job.UpdatedAtUtc = DateTime.UtcNow;
                    job.LastErrorMessage = ex.Message;

                    if (job.RetryCount < 3)
                    {
                        job.Status = JobStatus.Pending;
                        job.NextRetryAtUtc = DateTime.UtcNow.AddSeconds(30);
                    }
                    else
                    {
                        job.Status = JobStatus.Failed;
                        job.NextRetryAtUtc = null;
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);

                    var result = new JobResult
                    {
                        JobId = job.Id,
                        Success = false,
                        RetryCount = job.RetryCount,
                        StartedAtUtc = startAt,
                        FinishedAtUtc = DateTime.UtcNow,
                        ErrorMessage = ex.Message,
                    };

                    _logger.LogInformation("Job result: {@JobResult}", result);
                }
            }

            await Task.Delay(3000, stoppingToken);
        }
    }
}
