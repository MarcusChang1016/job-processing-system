using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Models;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Services;

public class JobExecutionService
{
    private readonly ILogger<JobExecutionService> _logger;
    private readonly WorkerOptions _workerOptions;
    private readonly JobRetryPolicy _jobRetryPolicy;
    private readonly Random _random = new();

    public JobExecutionService(
        ILogger<JobExecutionService> logger,
        IOptions<WorkerOptions> options,
        JobRetryPolicy jobRetryPolicy
    )
    {
        _logger = logger;
        _workerOptions = options.Value;
        _jobRetryPolicy = jobRetryPolicy;
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
            _jobRetryPolicy.ApplyFailure(job, ex.Message, DateTime.UtcNow);

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
