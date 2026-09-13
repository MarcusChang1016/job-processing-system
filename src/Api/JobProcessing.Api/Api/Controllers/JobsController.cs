using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace JobProcessing.Api.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController : ControllerBase
{
    private readonly GetJobService _getJobService;
    private readonly CreateJobService _createJobService;
    private readonly ManualJobRetryService _manualJobRetryService;

    private static ProblemDetails InvalidJobStateProblem() =>
        new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid job state",
            Detail = "Only failed jobs can be retried.",
        };

    public JobsController(
        GetJobService getJobService,
        CreateJobService createJobService,
        ManualJobRetryService manualJobRetryService
    )
    {
        _getJobService = getJobService;
        _createJobService = createJobService;
        _manualJobRetryService = manualJobRetryService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await _getJobService.GetAsync(id, cancellationToken);

        return job is null ? NotFound() : Ok(JobResponse.FromJobDetails(job));
    }

    [HttpPost]
    public async Task<IActionResult> CreateJob(CancellationToken cancellationToken)
    {
        var job = await _createJobService.CreateAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetJob),
            new { id = job.Id },
            JobResponse.FromJobDetails(job)
        );
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
