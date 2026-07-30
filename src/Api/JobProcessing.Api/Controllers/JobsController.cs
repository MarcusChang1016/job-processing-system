using JobProcessing.Api.Contracts;
using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using Microsoft.AspNetCore.Mvc;

namespace JobProcessing.Api.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    private static ProblemDetails InvalidJobStateProblem() =>
        new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid job state",
            Detail = "Only failed jobs can be retried.",
        };

    public JobsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
    public async Task<IActionResult> RetryJob(Guid id)
    {
        var job = await _dbContext.Jobs.FindAsync(id);

        if (job == null)
            return NotFound();

        if (job.Status != JobStatus.Failed)
        {
            return BadRequest(InvalidJobStateProblem());
        }

        job.Status = JobStatus.Pending;
        job.RetryCount = 0;
        job.UpdatedAtUtc = DateTime.UtcNow;
        job.NextRetryAtUtc = null;
        job.CompletedAtUtc = null;
        job.ProcessingStartedAtUtc = null;
        job.LastErrorMessage = null;

        _dbContext.Jobs.Update(job);
        await _dbContext.SaveChangesAsync();

        return Ok(JobResponse.FromEntity(job));
    }
}
