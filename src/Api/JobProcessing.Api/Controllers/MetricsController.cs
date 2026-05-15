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
        var pendingJobs = await _dbContext.Jobs.CountAsync(job => job.Status == "Pending");

        var processingJobs = await _dbContext.Jobs.CountAsync(job => job.Status == "Processing");

        var failedJobs = await _dbContext.Jobs.CountAsync(job => job.Status == "Failed");

        var successfulJobs = await _dbContext.Jobs.CountAsync(job => job.Status == "Succes");

        return Ok(
            new
            {
                pendingJobs,
                processingJobs,
                completedJobs,
                failedJobs,
                successfulJobs,
            }
        );
    }
}
