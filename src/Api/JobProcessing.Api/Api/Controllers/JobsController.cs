using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Contracts;
using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using Microsoft.AspNetCore.Mvc;

namespace JobProcessing.Api.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ManualJobRetryService _manualJobRetryService;

    private static ProblemDetails InvalidJobStateProblem() =>
        new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid job state",
            Detail = "Only failed jobs can be retried.",
        };

    public JobsController(AppDbContext dbContext, ManualJobRetryService manualJobRetryService)
    {
        _dbContext = dbContext;
        _manualJobRetryService = manualJobRetryService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await _dbContext.Jobs.FindAsync(id);

        if (job == null)
            return NotFound();

        return Ok(JobResponse.FromEntity(job));
    }

    [HttpPost]
    public async Task<IActionResult> CreateJob()
    {
        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            RetryCount = 0,
        };

        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, JobResponse.FromEntity(job));
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> RetryJob(Guid id, CancellationToken cancellationToken)
    {
        var result = await _manualJobRetryService.RetryAsync(id, cancellationToken);

        switch (result.Outcome)
        {
            case ManualJobRetryOutcome.NotFound:
                return NotFound();

            case ManualJobRetryOutcome.InvalidState:
                return BadRequest(InvalidJobStateProblem());

            case ManualJobRetryOutcome.Succeeded:
                if (result.Job is null)
                    throw new InvalidOperationException("Successful retry result has no job.");

                return Ok(JobResponse.FromJobDetails(result.Job));

            default:
                throw new InvalidOperationException(
                    $"Unknown manual retry outcome: {result.Outcome}"
                );
        }
    }
}
