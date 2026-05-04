namespace JobProcessing.Api.Services;

public class JobWorker : BackgroundService
{
    private readonly ILogger<JobWorker> _logger;
    private readonly JobStore _jobStore;

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
                _logger.LogInformation("Processing job {id}", job.Id);

                // Simulate job processing
                job.Status = "Processing";
                job.UpdatedAt = DateTime.UtcNow;
                _jobStore.UpdateJob(job);

                // Simulate processing time
                await Task.Delay(2000, stoppingToken);

                // Mark job as succeeded
                job.Status = "Succeeded";
                job.UpdatedAt = DateTime.UtcNow;
                _jobStore.UpdateJob(job);
                _logger.LogInformation("Job {id} completed successfully", job.Id);
            }

            _logger.LogInformation("JobWorker heartbeat at: {time}", DateTime.Now);

            await Task.Delay(3000, stoppingToken);
        }
    }

}