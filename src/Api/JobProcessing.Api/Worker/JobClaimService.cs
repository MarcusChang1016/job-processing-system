using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Worker;

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
        var pendingStatus = (int)JobStatus.Pending;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        var jobs = await _dbContext
            .Jobs.FromSqlInterpolated(
                $"""
                SELECT jobs.*, jobs.xmin
                FROM "Jobs" AS jobs
                WHERE jobs."Status" = {pendingStatus}
                  AND (jobs."NextRetryAtUtc" IS NULL OR jobs."NextRetryAtUtc" <= {now})
                  AND jobs."RetryCount" < {_options.MaxRetryCount}
                ORDER BY jobs."CreatedAtUtc"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """
            )
            .ToListAsync(cancellationToken);

        var job = jobs.SingleOrDefault();

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
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return job;
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Job {JobId} was claimed by another worker", job.Id);
                return null;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return null;
    }
}
