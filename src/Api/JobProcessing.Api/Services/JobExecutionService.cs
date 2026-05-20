using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Models;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Services;

public class JobExecutionService
{
    private readonly ILogger<JobExecutionService> _logger;
    private readonly WorkerOptions _workerOptions;
    private readonly Random _random = new();

    public JobExecutionService(ILogger<JobExecutionService> logger, IOptions<WorkerOptions> options)
    {
        _logger = logger;
        _workerOptions = options.Value;
    }

    public async Task ExecuteAsync(JobEntity job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing job {JobId}", job.Id);

        var startAt = DateTime.UtcNow;

        try
        {
            // Simulate processing time
            await Task.Delay(_workerOptions.ProcessingDelaySeconds * 1000, stoppingToken);

            // Simulate random failure / success
            bool isFailed = _random.Next(0, 2) == 0; // 50% chance of failure

            if (isFailed)
                throw new Exception("Simulated failure");

            job.Status = JobStatus.Success;
            job.UpdatedAtUtc = DateTime.UtcNow;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.LastErrorMessage = null;

            _logger.LogInformation("Job {id} completed successfully", job.Id);
            _logger.LogInformation(
                "JobResult {@JobResult}",
                new JobResult
                {
                    JobId = job.Id,
                    Success = true,
                    RetryCount = job.RetryCount,
                    StartedAtUtc = startAt,
                    FinishedAtUtc = DateTime.UtcNow,
                }
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            job.RetryCount += 1;
            job.UpdatedAtUtc = DateTime.UtcNow;
            job.LastErrorMessage = ex.Message;

            if (job.RetryCount < _workerOptions.MaxRetryCount)
            {
                job.Status = JobStatus.Pending;
                job.NextRetryAtUtc = DateTime.UtcNow.AddSeconds(
                    _workerOptions.RetryCooldownSeconds
                );
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.NextRetryAtUtc = null;
            }

            _logger.LogInformation(
                "Job result: {@JobResult}",
                new JobResult
                {
                    JobId = job.Id,
                    Success = false,
                    RetryCount = job.RetryCount,
                    StartedAtUtc = startAt,
                    FinishedAtUtc = DateTime.UtcNow,
                    ErrorMessage = ex.Message,
                }
            );
        }
    }
}
