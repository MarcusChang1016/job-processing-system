using JobProcessing.Api.Domain.Jobs;

namespace JobProcessing.Api.Application.Jobs;

public sealed record JobDetails(
    Guid Id,
    JobStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int RetryCount,
    DateTime? CompletedAtUtc
);
