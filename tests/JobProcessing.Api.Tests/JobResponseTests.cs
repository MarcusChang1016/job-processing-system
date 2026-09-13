using FluentAssertions;
using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Contracts;
using JobProcessing.Api.Domain.Jobs;

namespace JobProcessing.Api.Tests;

public class JobResponseTests
{
    [Fact]
    public void FromJobDetails_ShouldMapBasicFields_WhenJobIsPending()
    {
        var details = new JobDetails(
            Id: Guid.NewGuid(),
            Status: JobStatus.Pending,
            CreatedAtUtc: new DateTime(2026, 07, 03, 23, 45, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc: new DateTime(2026, 07, 03, 23, 45, 1, 0, DateTimeKind.Utc),
            RetryCount: 2,
            CompletedAtUtc: null
        );

        var response = JobResponse.FromJobDetails(details);

        response.Id.Should().Be(details.Id);
        response.Status.Should().Be("Pending");
        response.CreatedAt.Should().Be(details.CreatedAtUtc);
        response.UpdatedAt.Should().Be(details.UpdatedAtUtc);
        response.RetryCount.Should().Be(details.RetryCount);
        response.CompletedAt.Should().BeNull();
        response.FailureReason.Should().BeNull();
    }

    [Fact]
    public void FromJobDetails_ShouldUseGenericFailureReason_WhenJobIsFailed()
    {
        var details = new JobDetails(
            Id: Guid.NewGuid(),
            Status: JobStatus.Failed,
            CreatedAtUtc: new DateTime(2026, 07, 03, 23, 45, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc: new DateTime(2026, 07, 03, 23, 45, 1, 0, DateTimeKind.Utc),
            RetryCount: 2,
            CompletedAtUtc: null
        );

        var response = JobResponse.FromJobDetails(details);

        response.Status.Should().Be("Failed");
        response.FailureReason.Should().Be("Job failed after maximum retries.");
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.Success)]
    public void FromJobDetails_ShouldNotHaveFailureReason_WhenJobIsNotFailed(JobStatus status)
    {
        var details = new JobDetails(
            Id: Guid.NewGuid(),
            Status: status,
            CreatedAtUtc: new DateTime(2026, 07, 03, 23, 45, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc: new DateTime(2026, 07, 03, 23, 45, 1, 0, DateTimeKind.Utc),
            RetryCount: 2,
            CompletedAtUtc: null
        );

        var response = JobResponse.FromJobDetails(details);

        response.FailureReason.Should().BeNull();
    }

    [Fact]
    public void FromJobDetails_ShouldMapCompletedAt_WhenJobHasCompletedAtUtc()
    {
        var details = new JobDetails(
            Id: Guid.NewGuid(),
            Status: JobStatus.Success,
            CreatedAtUtc: new DateTime(2026, 07, 03, 23, 45, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc: new DateTime(2026, 07, 03, 23, 45, 1, 0, DateTimeKind.Utc),
            RetryCount: 2,
            CompletedAtUtc: new DateTime(2026, 07, 04, 00, 01, 0, 0, DateTimeKind.Utc)
        );

        var response = JobResponse.FromJobDetails(details);

        response.CompletedAt.Should().Be(details.CompletedAtUtc);
    }
}
