using Microsoft.AspNetCore.Mvc;
using JobProcessing.Api.Models;

namespace JobProcessing.Api.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController : ControllerBase
{
    private static readonly List<Job> _jobs = new();

    [HttpGet("{id}")]
    public IActionResult GetJob(Guid id)
    {
        var job = _jobs.FirstOrDefault(j => j.Id == id);

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

        _jobs.Add(job);

        return Ok(new
        {
            id = job.Id,
            status = job.Status,
        });
    }
}