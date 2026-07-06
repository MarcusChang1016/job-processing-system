using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Models;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Services;

public class JobExecutionService
{
    private readonly ILogger<JobExecutionService> _logger;
    private readonly WorkerOptions _workerOptions;
    private readonly JobExecutionResultHandler _jobExecutionResultHandler;
    private readonly TimeProvider _timeProvider;
    private readonly Random _random = new();

    public JobExecutionService(
        ILogger<JobExecutionService> logger,
        IOptions<WorkerOptions> options,
        JobExecutionResultHandler jobExecutionResultHandler,
        TimeProvider timeProvider
    )
    {
        _logger = logger;
        _workerOptions = options.Value;
        _jobExecutionResultHandler = jobExecutionResultHandler;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(JobEntity job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing job {JobId}", job.Id);

        var startAt = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            // Simulate processing time
            await Task.Delay(_workerOptions.ProcessingDelaySeconds * 1000, stoppingToken);

            // Simulate random failure / success
            bool isFailed = _random.Next(0, 2) == 0; // 50% chance of failure

            if (isFailed)
                throw new Exception("Simulated failure");

            var finishedAt = _timeProvider.GetUtcNow().UtcDateTime;

            _jobExecutionResultHandler.ApplySuccess(job, finishedAt);

            _logger.LogInformation("Job {id} completed successfully", job.Id);
            _logger.LogInformation(
                "JobResult {@JobResult}",
                new JobResult
                {
                    JobId = job.Id,
                    Success = true,
                    RetryCount = job.RetryCount,
                    StartedAtUtc = startAt,
                    FinishedAtUtc = finishedAt,
                }
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var finishedAt = _timeProvider.GetUtcNow().UtcDateTime;

            _jobExecutionResultHandler.ApplyFailure(job, ex.Message, finishedAt);

            _logger.LogInformation(
                "Job result: {@JobResult}",
                new JobResult
                {
                    JobId = job.Id,
                    Success = false,
                    RetryCount = job.RetryCount,
                    StartedAtUtc = startAt,
                    FinishedAtUtc = finishedAt,
                    ErrorMessage = ex.Message,
                }
            );
        }
    }
}
