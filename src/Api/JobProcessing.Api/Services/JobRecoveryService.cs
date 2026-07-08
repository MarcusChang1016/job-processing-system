using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Services;

public class JobRecoveryService
{
    private readonly AppDbContext _dbContext;
    private readonly WorkerOptions _options;
    private readonly JobRetryPolicy _jobRetryPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JobRecoveryService> _logger;

    public JobRecoveryService(
        AppDbContext dbContext,
        IOptions<WorkerOptions> options,
        JobRetryPolicy jobRetryPolicy,
        TimeProvider timeProvider,
        ILogger<JobRecoveryService> logger
    )
    {
        _dbContext = dbContext;
        _options = options.Value;
        _jobRetryPolicy = jobRetryPolicy;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> RecoverStuckJobsAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var timeoutThreshold = now.AddSeconds(-_options.StuckJobTimeoutSeconds);

        var stuckJobs = await _dbContext
            .Jobs.Where(job =>
                job.Status == JobStatus.Processing
                && job.ProcessingStartedAtUtc != null
                && job.ProcessingStartedAtUtc < timeoutThreshold
            )
            .ToListAsync(cancellationToken);

        foreach (var stuckJob in stuckJobs)
        {
            _logger.LogWarning("Recovering stuck job {id}", stuckJob.Id);

            _jobRetryPolicy.ApplyFailedAttempt(
                stuckJob,
                "Recovered from stale processing state",
                now
            );
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return stuckJobs.Count;
    }
}
