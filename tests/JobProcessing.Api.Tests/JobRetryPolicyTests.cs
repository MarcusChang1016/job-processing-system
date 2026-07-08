using FluentAssertions;
using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Models;
using JobProcessing.Api.Services;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Tests;

public class JobRetryPolicyTests
{
    [Fact]
    public void ApplyFailedAttempt_ShouldSetJobToPending_WhenRetryCountIsBelowMaxRetryCount()
    {
        var options = Options.Create(
            new WorkerOptions { MaxRetryCount = 3, RetryCooldownSeconds = 30 }
        );

        var policy = new JobRetryPolicy(options);

        var now = new DateTime(2026, 07, 05, 18, 00, 0, 0, DateTimeKind.Utc);
        var errorMessage = "Simulated failure";

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Processing,
            RetryCount = 0,
            ProcessingStartedAtUtc = now.AddMinutes(5),
        };

        policy.ApplyFailedAttempt(job, errorMessage, now);

        job.RetryCount.Should().Be(1);
        job.Status.Should().Be(JobStatus.Pending);
        job.UpdatedAtUtc.Should().Be(now);
        job.LastErrorMessage.Should().Be(errorMessage);
        job.NextRetryAtUtc.Should().Be(now.AddSeconds(30));
        job.ProcessingStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApplyFailedAttempt_ShouldSetJobToFailed_WhenRetryCountReachesMaxRetryCount()
    {
        var options = Options.Create(
            new WorkerOptions { MaxRetryCount = 3, RetryCooldownSeconds = 30 }
        );

        var policy = new JobRetryPolicy(options);

        var now = new DateTime(2026, 07, 05, 18, 00, 0, 0, DateTimeKind.Utc);
        var errorMessage = "Simulated failure";

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Processing,
            RetryCount = 2,
            ProcessingStartedAtUtc = now.AddMinutes(5),
        };

        policy.ApplyFailedAttempt(job, errorMessage, now);

        job.RetryCount.Should().Be(3);
        job.Status.Should().Be(JobStatus.Failed);
        job.UpdatedAtUtc.Should().Be(now);
        job.LastErrorMessage.Should().Be(errorMessage);
        job.NextRetryAtUtc.Should().BeNull();
        job.ProcessingStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApplyFailedAttempt_ShouldSetJobToFailed_WhenMaxRetryCountIsOne()
    {
        var options = Options.Create(
            new WorkerOptions { MaxRetryCount = 1, RetryCooldownSeconds = 30 }
        );

        var policy = new JobRetryPolicy(options);

        var now = new DateTime(2026, 07, 05, 18, 00, 0, 0, DateTimeKind.Utc);
        var errorMessage = "Simulated failure";

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Processing,
            RetryCount = 0,
            ProcessingStartedAtUtc = now.AddMinutes(5),
        };

        policy.ApplyFailedAttempt(job, errorMessage, now);

        job.RetryCount.Should().Be(1);
        job.Status.Should().Be(JobStatus.Failed);
        job.UpdatedAtUtc.Should().Be(now);
        job.LastErrorMessage.Should().Be(errorMessage);
        job.NextRetryAtUtc.Should().BeNull();
        job.ProcessingStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApplyFailedAttempt_ShouldSetNextRetryAtUtcToNow_WhenRetryCooldownSecondsIsZero()
    {
        var options = Options.Create(
            new WorkerOptions { MaxRetryCount = 3, RetryCooldownSeconds = 0 }
        );

        var policy = new JobRetryPolicy(options);

        var now = new DateTime(2026, 07, 05, 18, 00, 0, 0, DateTimeKind.Utc);
        var errorMessage = "Simulated failure";

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Processing,
            RetryCount = 0,
            ProcessingStartedAtUtc = now.AddMinutes(5),
        };

        policy.ApplyFailedAttempt(job, errorMessage, now);

        job.RetryCount.Should().Be(1);
        job.Status.Should().Be(JobStatus.Pending);
        job.UpdatedAtUtc.Should().Be(now);
        job.LastErrorMessage.Should().Be(errorMessage);
        job.NextRetryAtUtc.Should().Be(now);
        job.ProcessingStartedAtUtc.Should().BeNull();
    }
}
