using JobProcessing.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobProcessing.Api.Application.Jobs;

public sealed class GetJobService
{
    private readonly AppDbContext _dbContext;

    public GetJobService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<JobDetails?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext
            .Jobs.AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == jobId, cancellationToken);

        if (job is null)
            return null;

        return new JobDetails(
            job.Id,
            job.Status,
            job.CreatedAtUtc,
            job.UpdatedAtUtc,
            job.RetryCount,
            job.CompletedAtUtc,
            job.WeatherObservedAtUtc,
            job.AirTemperatureCelsius
        );
    }
}
