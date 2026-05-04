using Microsoft.AspNetCore.Mvc;
using JobProcessing.Api.Models;
using JobProcessing.Api.Services;

namespace JobProcessing.Api.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController : ControllerBase
{
    private readonly JobStore _jobStore;

    public JobsController(JobStore jobStore)
    {
        _jobStore = jobStore;
    }

    [HttpGet("{id}")]
    public IActionResult GetJob(Guid id)
    {
        var job = _jobStore.GetJob(id);

        if (job == null) return NotFound();

        return Ok(new
        {
            id = job.Id,
            status = job.Status,
            createdAt = job.CreatedAt,
            updatedAt = job.UpdatedAt,
            retryCount = job.RetryCount
        });
    }

    [HttpPost]
    public IActionResult CreateJob()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        _jobStore.AddJob(job);

        return Ok(new
        {
            id = job.Id,
            status = job.Status,
        });
    }

    [HttpPost("{id}/retry")]
    public IActionResult RetryJob(Guid id)
    {
        var job = _jobStore.GetJob(id);

        if (job == null) return NotFound();

        if (job.Status != "Failed") return BadRequest("Only failed jobs can be retried.");

        job.Status = "Pending";
        job.RetryCount += 1;
        job.UpdatedAt = DateTime.UtcNow;

        _jobStore.UpdateJob(job);

        return Ok(new
        {
            id = job.Id,
            status = job.Status,
            retryCount = job.RetryCount
        });
    }
}