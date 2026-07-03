using FluentAssertions;
using JobProcessing.Api.Contracts;
using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure.Entities;

namespace JobProcessing.Api.Tests;

public class JobResponseTests
{
    [Fact]
    public void FromEntity_ShouldMapBasicFields_WhenJobIsPending()
    {
        var entity = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Pending,
            CreatedAtUtc = new DateTime(2026, 07, 03, 23, 45, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 07, 03, 23, 45, 1, 0, DateTimeKind.Utc),
            RetryCount = 2,
            CompletedAtUtc = null,
            LastErrorMessage = null,
        };

        var response = JobResponse.FromEntity(entity);

        response.Id.Should().Be(entity.Id);
        response.Status.Should().Be("Pending");
        response.CreatedAt.Should().Be(entity.CreatedAtUtc);
        response.UpdatedAt.Should().Be(entity.UpdatedAtUtc);
        response.RetryCount.Should().Be(entity.RetryCount);
        response.CompletedAt.Should().BeNull();
        response.FailureReason.Should().BeNull();
    }

    [Fact]
    public void FromEntity_ShouldUseGenericFailureReason_WhenJobIsFailed()
    {
        var entity = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Failed,
            CreatedAtUtc = new DateTime(2026, 07, 03, 23, 45, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 07, 03, 23, 45, 1, 0, DateTimeKind.Utc),
            RetryCount = 2,
            CompletedAtUtc = null,
            LastErrorMessage = "Database timeout while calling external service",
        };

        var response = JobResponse.FromEntity(entity);

        response.Status.Should().Be("Failed");
        response.FailureReason.Should().Be("Job failed after maximum retries.");
        response.FailureReason.Should().NotBe(entity.LastErrorMessage);
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.Success)]
    public void FromEntity_ShouldNotHaveFailureReason_WhenJobIsNotFailed(JobStatus status)
    {
        var entity = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = status,
            CreatedAtUtc = new DateTime(2026, 07, 03, 23, 45, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 07, 03, 23, 45, 1, 0, DateTimeKind.Utc),
            RetryCount = 2,
            CompletedAtUtc = null,
            LastErrorMessage = "Database timeout while calling external service",
        };

        var response = JobResponse.FromEntity(entity);

        response.FailureReason.Should().BeNull();
    }

    [Fact]
    public void FromEntity_ShouldMapCompletedAt_WhenJobHasCompletedAtUtc()
    {
        var entity = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Success,
            CreatedAtUtc = new DateTime(2026, 07, 03, 23, 45, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 07, 03, 23, 45, 1, 0, DateTimeKind.Utc),
            RetryCount = 2,
            CompletedAtUtc = new DateTime(2026, 07, 04, 00, 01, 0, 0, DateTimeKind.Utc),
        };

        var response = JobResponse.FromEntity(entity);

        response.CompletedAt.Should().Be(entity.CompletedAtUtc);
    }
}
