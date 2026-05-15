using System.ComponentModel.DataAnnotations;
using JobProcessing.Api.Enums;

namespace JobProcessing.Api.Infrastructure.Entities;

public class JobEntity
{
    public Guid Id { get; set; }

    public JobStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }

    public int RetryCount { get; set; }

    public DateTime? ProcessingStartedAtUtc { get; set; }

    public string? LastErrorMessage { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = default!;
}
