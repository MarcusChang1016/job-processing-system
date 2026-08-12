using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure;

namespace JobProcessing.Api.Application.Jobs;

public class ManualJobRetryService
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public ManualJobRetryService(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<ManualJobRetryResult> RetryAsync(
        Guid jobId,
        CancellationToken cancellationToken
    )
    {
        var job = await _dbContext.Jobs.FindAsync([jobId], cancellationToken);

        if (job == null)
            return ManualJobRetryResult.NotFound();

        if (job.Status != JobStatus.Failed)
            return ManualJobRetryResult.InvalidState();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        job.Status = JobStatus.Pending;
        job.RetryCount = 0;
        job.UpdatedAtUtc = now;
        job.NextRetryAtUtc = null;
        job.CompletedAtUtc = null;
        job.ProcessingStartedAtUtc = null;
        job.LastErrorMessage = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ManualJobRetryResult.Succeeded(job);
    }
}
