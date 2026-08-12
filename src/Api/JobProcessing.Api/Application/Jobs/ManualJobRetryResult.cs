using JobProcessing.Api.Infrastructure.Entities;

namespace JobProcessing.Api.Application.Jobs;

public sealed class ManualJobRetryResult
{
    private ManualJobRetryResult(ManualJobRetryOutcome outcome, JobEntity? job)
    {
        Outcome = outcome;
        Job = job;
    }

    public ManualJobRetryOutcome Outcome { get; }

    public JobEntity? Job { get; }

    public static ManualJobRetryResult Succeeded(JobEntity job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return new(ManualJobRetryOutcome.Succeeded, job);
    }

    public static ManualJobRetryResult NotFound() => new(ManualJobRetryOutcome.NotFound, null);

    public static ManualJobRetryResult InvalidState() =>
        new(ManualJobRetryOutcome.InvalidState, null);
}
