using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure.Entities;

namespace JobProcessing.Api.Contracts;

public record JobResponse
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int RetryCount { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? FailureReason { get; init; }

    public static JobResponse FromEntity(JobEntity entity) =>
        new()
        {
            Id = entity.Id,
            Status = entity.Status.ToString(),
            CreatedAt = entity.CreatedAtUtc,
            UpdatedAt = entity.UpdatedAtUtc,
            RetryCount = entity.RetryCount,
            CompletedAt = entity.CompletedAtUtc,
            FailureReason =
                entity.Status == JobStatus.Failed ? "Job failed after maximum retries." : null,
        };
}
