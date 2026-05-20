namespace JobProcessing.Api.Models;

public class WorkerOptions
{
    public int PollingIntervalSeconds { get; set; }

    public int ProcessingDelaySeconds { get; set; }

    public int RetryCooldownSeconds { get; set; }

    public int MaxRetryCount { get; set; }

    public int StuckJobTimeoutSeconds { get; set; }
}
