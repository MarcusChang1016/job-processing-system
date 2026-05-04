using JobProcessing.Api.Models;

namespace JobProcessing.Api.Services;

public class JobWorker : BackgroundService
{
    private readonly ILogger<JobWorker> _logger;
    private readonly JobStore _jobStore;
    private readonly Random _random = new();

    public JobWorker(JobStore jobStore, ILogger<JobWorker> logger)
    {
        _jobStore = jobStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobWorker started at: {time}", DateTime.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            var job = _jobStore.GetPendingJobs().FirstOrDefault();

            if (job != null)
            {
                var startAt = DateTime.UtcNow;

                _logger.LogInformation("Processing job {id}", job.Id);

                // Simulate job processing
                job.Status = "Processing";
                job.UpdatedAt = DateTime.UtcNow;
                _jobStore.UpdateJob(job);

                // Simulate processing time
                await Task.Delay(2000, stoppingToken);

                // Simulate random failure / success
                bool isFailed = _random.Next(0, 2) == 0; // 50% chance of failure

                JobResult result;

                if (isFailed)
                {
                    job.Status = "Failed";
                    job.UpdatedAt = DateTime.UtcNow;
                    _jobStore.UpdateJob(job);

                    _logger.LogWarning("Job {id} failed", job.Id);

                    result = new JobResult
                    {
                        JobId = job.Id,
                        Success = false,
                        RetryCount = job.RetryCount,
                        StartAtUtc = startAt,
                        FinishedAtUtc = DateTime.UtcNow,
                        ErrorMessage = "Simulated random failure"
                    };

                }
                else
                {
                    job.Status = "Succeeded";
                    job.UpdatedAt = DateTime.UtcNow;
                    _jobStore.UpdateJob(job);

                    _logger.LogInformation("Job {id} completed successfully", job.Id);

                    result = new JobResult
                    {
                        JobId = job.Id,
                        Success = true,
                        RetryCount = job.RetryCount,
                        StartAtUtc = startAt,
                        FinishedAtUtc = DateTime.UtcNow
                    };
                }

                _logger.LogInformation("Job result: {@JobResult}", result);
            }

            await Task.Delay(3000, stoppingToken);
        }
    }

}