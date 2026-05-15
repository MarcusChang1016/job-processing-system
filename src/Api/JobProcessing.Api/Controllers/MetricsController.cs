using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobProcessing.Api.Controllers;

[ApiController]
[Route("metrics")]
public class MetricsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public MetricsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetMetrics()
    {
        var pendingJobs = await _dbContext.Jobs.CountAsync(job => job.Status == JobStatus.Pending);

        var processingJobs = await _dbContext.Jobs.CountAsync(job =>
            job.Status == JobStatus.Processing
        );

        var failedJobs = await _dbContext.Jobs.CountAsync(job => job.Status == JobStatus.Failed);

        var successfulJobs = await _dbContext.Jobs.CountAsync(job =>
            job.Status == JobStatus.Success
        );

        return Ok(
            new
            {
                pendingJobs,
                processingJobs,
                failedJobs,
                successfulJobs,
            }
        );
    }
}
