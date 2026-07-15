using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;

namespace JobProcessing.Api.Services;

public class JobProcessor
{
    private readonly ILogger<JobProcessor> _logger;
    private readonly JobExecutionService _jobExecutionService;
    private readonly AppDbContext _dbContext;

    public JobProcessor(
        ILogger<JobProcessor> logger,
        JobExecutionService jobExecutionService,
        AppDbContext dbContext
    )
    {
        _logger = logger;
        _jobExecutionService = jobExecutionService;
        _dbContext = dbContext;
    }

    public async Task ProcessAsync(JobEntity job, CancellationToken cancellationToken)
    {
        if (job.Status != JobStatus.Processing)
        {
            _logger.LogWarning(
                "Skipping job {JobId} because status is {Status}, expected {ExpectedStatus}",
                job.Id,
                job.Status,
                JobStatus.Processing
            );

            return;
        }

        await _jobExecutionService.ExecuteAsync(job, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
