namespace JobProcessing.Api.Application.Jobs;

public sealed class ManualJobRetryResult
{
    private ManualJobRetryResult(ManualJobRetryOutcome outcome, JobDetails? job)
    {
        Outcome = outcome;
        Job = job;
    }

    public ManualJobRetryOutcome Outcome { get; }

    public JobDetails? Job { get; }

    public static ManualJobRetryResult Succeeded(JobDetails job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return new(ManualJobRetryOutcome.Succeeded, job);
    }

    public static ManualJobRetryResult NotFound() => new(ManualJobRetryOutcome.NotFound, null);

    public static ManualJobRetryResult InvalidState() =>
        new(ManualJobRetryOutcome.InvalidState, null);
}
