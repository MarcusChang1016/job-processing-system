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
            var job = await dbContext.Jobs
                .Where(j => j.Status == "Pending")
                .OrderBy(j => j.CreatedAtUtc)
                .FirstOrDefaultAsync(stoppingToken);


            if (job != null)
            {
                var startAt = DateTime.UtcNow;

                try
                {
                    _logger.LogInformation("Processing job {id}", job.Id);

                    // Simulate job processing
                    job.Status = "Processing";
                    job.ProcessingStartedAtUtc = DateTime.UtcNow;
                    job.UpdatedAtUtc = DateTime.UtcNow;
                    dbContext.Jobs.Update(job);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    // Simulate processing time
                    await Task.Delay(2000, stoppingToken);

                    // Simulate random failure / success
                    bool isFailed = _random.Next(0, 2) == 0; // 50% chance of failure

                    if (isFailed) throw new Exception("Simulated failure");

                    job.Status = "Succeeded";
                    job.UpdatedAtUtc = DateTime.UtcNow;
                    job.LastErrorMessage = null;
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Job {id} completed successfully", job.Id);

                    var result = new JobResult
                    {
                        JobId = job.Id,
                        Success = true,
                        RetryCount = job.RetryCount,
                        StartedAtUtc = startAt,
                        FinishedAtUtc = DateTime.UtcNow
                    };

                    _logger.LogInformation("JobResult {@JobResult}", result);
                }
                catch (Exception ex)
                {
                    job.Status = "Failed";
                    job.UpdatedAtUtc = DateTime.UtcNow;
                    job.LastErrorMessage = ex.Message;
                    await dbContext.SaveChangesAsync(stoppingToken);

                    var result = new JobResult
                    {
                        JobId = job.Id,
                        Success = false,
                        RetryCount = job.RetryCount,
                        StartedAtUtc = startAt,
                        FinishedAtUtc = DateTime.UtcNow,
                        ErrorMessage = ex.Message
                    };

                    _logger.LogInformation("Job result: {@JobResult}", result);
                }
            }

            await Task.Delay(3000, stoppingToken);
        }
    }

}