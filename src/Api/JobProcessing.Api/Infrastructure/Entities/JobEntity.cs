namespace JobProcessing.Api.Infrastructure.Entities;

public class JobEntity
{
    public Guid Id { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }

    public int RetryCount { get; set; }

    public DateTime? ProcessingStartedAtUtc { get; set; }

    public string? LastErrorMessage { get; set; }
}
