namespace JobProcessing.Api.Contracts;

public record MetricsResponse
{
    public int PendingJobs { get; init; }
    public int ProcessingJobs { get; init; }
    public int FailedJobs { get; init; }
    public int SuccessfulJobs { get; init; }
}
