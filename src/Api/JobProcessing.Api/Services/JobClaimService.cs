using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Services;

public class JobClaimService
{
    private readonly AppDbContext _dbContext;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JobClaimService> _logger;

    public JobClaimService(
        AppDbContext dbContext,
        IOptions<WorkerOptions> options,
        TimeProvider timeProvider,
        ILogger<JobClaimService> logger
    )
    {
        _dbContext = dbContext;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<JobEntity?> ClaimNextJobAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var job = await _dbContext
            .Jobs.Where(job =>
                job.Status == JobStatus.Pending
                && (job.NextRetryAtUtc == null || job.NextRetryAtUtc <= now)
                && (job.RetryCount < _options.MaxRetryCount)
            )
            .OrderBy(job => job.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (job != null)
        {
            _logger.LogInformation("Claiming job {id}", job.Id);

            if (!JobStateMachine.CanTransition(job.Status, JobStatus.Processing))
            {
                _logger.LogWarning(
                    "Invalid transition {Current} -> {Next} for job {JobId}",
                    job.Status,
                    JobStatus.Processing,
                    job.Id
                );

                return null;
            }

            job.Status = JobStatus.Processing;
            job.ProcessingStartedAtUtc = now;
            job.UpdatedAtUtc = now;

            try
            {
                _dbContext.Jobs.Update(job);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return job;
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Job {JobId} was claimed by another worker", job.Id);
                return null;
            }
        }

        return null;
    }
}
