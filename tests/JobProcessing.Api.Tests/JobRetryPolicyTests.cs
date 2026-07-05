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
    public void ApplyFailure_ShouldSetJobToPending_WhenRetryCountIsBelowMaxRetryCount()
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
        };

        policy.ApplyFailure(job, errorMessage, now);

        job.RetryCount.Should().Be(1);
        job.Status.Should().Be(JobStatus.Pending);
        job.UpdatedAtUtc.Should().Be(now);
        job.LastErrorMessage.Should().Be(errorMessage);
        job.NextRetryAtUtc.Should().Be(now.AddSeconds(30));
    }

    [Fact]
    public void ApplyFailure_ShouldSetJobToFailed_WhenRetryCountReachesMaxRetryCount()
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
        };

        policy.ApplyFailure(job, errorMessage, now);

        job.RetryCount.Should().Be(3);
        job.Status.Should().Be(JobStatus.Failed);
        job.UpdatedAtUtc.Should().Be(now);
        job.LastErrorMessage.Should().Be(errorMessage);
        job.NextRetryAtUtc.Should().BeNull();
    }
}
