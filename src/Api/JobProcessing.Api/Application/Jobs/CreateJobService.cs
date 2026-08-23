using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;

namespace JobProcessing.Api.Application.Jobs;

public sealed class CreateJobService
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CreateJobService(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<JobDetails> CreateAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            RetryCount = 0,
        };

        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new JobDetails(
            job.Id,
            job.Status,
            job.CreatedAtUtc,
            job.UpdatedAtUtc,
            job.RetryCount,
            job.CompletedAtUtc
        );
    }
}
