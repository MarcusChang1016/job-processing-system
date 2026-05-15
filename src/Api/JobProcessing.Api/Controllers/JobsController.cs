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

        return Ok(
            new
            {
                id = job.Id,
                status = job.Status,
                createdAt = job.CreatedAtUtc,
                updatedAt = job.UpdatedAtUtc,
                retryCount = job.RetryCount,
            }
        );
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

        return Ok(
            new
            {
                id = job.Id,
                status = job.Status,
                retryCount = job.RetryCount,
            }
        );
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> RetryJob(Guid id)
    {
        var job = await _dbContext.Jobs.FindAsync(id);

        if (job == null)
            return NotFound();

        if (job.Status != JobStatus.Failed)
            return BadRequest("Only failed jobs can be retried.");

        job.Status = JobStatus.Pending;
        job.RetryCount += 1;
        job.UpdatedAtUtc = DateTime.UtcNow;
        job.NextRetryAtUtc = null;

        _dbContext.Jobs.Update(job);
        await _dbContext.SaveChangesAsync();

        return Ok(
            new
            {
                id = job.Id,
                status = job.Status,
                retryCount = job.RetryCount,
            }
        );
    }
}
