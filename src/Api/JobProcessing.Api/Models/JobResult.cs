namespace JobProcessing.Api.Models;

public record JobResult
{
    public Guid JobId { get; set; }
    public bool Success { get; set; }
    public int RetryCount { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}