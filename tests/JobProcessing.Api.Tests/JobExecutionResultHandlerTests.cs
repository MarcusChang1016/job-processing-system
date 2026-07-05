using FluentAssertions;
using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Models;
using JobProcessing.Api.Services;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Tests;

public class JobExecutionResultHandlerTests
{
    [Fact]
    public void ApplySuccess_ShouldApplySuccessStateToJob()
    {
        var options = Options.Create(
            new WorkerOptions { MaxRetryCount = 1, RetryCooldownSeconds = 30 }
        );

        var retryPolicy = new JobRetryPolicy(options);

        var resultHandler = new JobExecutionResultHandler(retryPolicy);

        var now = new DateTime(2026, 07, 06, 00, 00, 0, 0, DateTimeKind.Utc);

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Processing,
            LastErrorMessage = "Previous failure",
            RetryCount = 0,
        };

        resultHandler.ApplySuccess(job, now);

        job.Status.Should().Be(JobStatus.Success);
        job.UpdatedAtUtc.Should().Be(now);
        job.CompletedAtUtc.Should().Be(now);
        job.LastErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ApplyFailure_ShouldMarkJobAsFailed_WhenRetryPolicyReachesMaxRetryCount()
    {
        var options = Options.Create(
            new WorkerOptions { MaxRetryCount = 1, RetryCooldownSeconds = 30 }
        );

        var retryPolicy = new JobRetryPolicy(options);

        var resultHandler = new JobExecutionResultHandler(retryPolicy);

        var now = new DateTime(2026, 07, 06, 00, 00, 0, 0, DateTimeKind.Utc);
        var errorMessage = "Simulated failure";

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Processing,
            RetryCount = 0,
        };

        resultHandler.ApplyFailure(job, errorMessage, now);

        job.RetryCount.Should().Be(1);
        job.Status.Should().Be(JobStatus.Failed);
        job.UpdatedAtUtc.Should().Be(now);
        job.LastErrorMessage.Should().Be(errorMessage);
        job.NextRetryAtUtc.Should().BeNull();
        job.CompletedAtUtc.Should().BeNull();
    }
}
