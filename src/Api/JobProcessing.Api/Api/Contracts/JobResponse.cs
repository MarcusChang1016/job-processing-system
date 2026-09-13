using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Domain.Jobs;

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

    public static JobResponse FromJobDetails(JobDetails job) =>
        new()
        {
            Id = job.Id,
            Status = job.Status.ToString(),
            CreatedAt = job.CreatedAtUtc,
            UpdatedAt = job.UpdatedAtUtc,
            RetryCount = job.RetryCount,
            CompletedAt = job.CompletedAtUtc,
            FailureReason =
                job.Status == JobStatus.Failed ? "Job failed after maximum retries." : null,
        };
}
