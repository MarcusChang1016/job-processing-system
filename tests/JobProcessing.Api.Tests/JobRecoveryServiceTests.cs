using FluentAssertions;
using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Models;
using JobProcessing.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Tests;

public class JobRecoveryServiceTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task RecoverStuckJobsAsync_ShouldRecoverOnlyTimedOutProcessingJobs()
    {
        var now = new DateTimeOffset(2026, 07, 07, 22, 0, 0, TimeSpan.Zero);

        var workerOptions = Options.Create(
            new WorkerOptions
            {
                MaxRetryCount = 3,
                RetryCooldownSeconds = 30,
                StuckJobTimeoutSeconds = 60,
            }
        );

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        await using var dbContext = new AppDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();

        var stuckJobId = Guid.NewGuid();
        var recentProcessingJobId = Guid.NewGuid();
        var pendingJobId = Guid.NewGuid();
        var failedJobId = Guid.NewGuid();

        dbContext.Jobs.AddRange(
            new JobEntity
            {
                Id = stuckJobId,
                Status = JobStatus.Processing,
                RetryCount = 0,
                ProcessingStartedAtUtc = now.UtcDateTime.AddMinutes(-2),
            },
            new JobEntity
            {
                Id = recentProcessingJobId,
                Status = JobStatus.Processing,
                RetryCount = 0,
                ProcessingStartedAtUtc = now.UtcDateTime.AddSeconds(-30),
            },
            new JobEntity
            {
                Id = pendingJobId,
                Status = JobStatus.Pending,
                RetryCount = 0,
                ProcessingStartedAtUtc = now.UtcDateTime.AddMinutes(-2),
            },
            new JobEntity
            {
                Id = failedJobId,
                Status = JobStatus.Failed,
                RetryCount = 0,
                ProcessingStartedAtUtc = now.UtcDateTime.AddMinutes(-2),
            }
        );

        await dbContext.SaveChangesAsync();

        var retryPolicy = new JobRetryPolicy(workerOptions);

        var service = new JobRecoveryService(
            dbContext,
            workerOptions,
            retryPolicy,
            new FixedTimeProvider(now),
            NullLogger<JobRecoveryService>.Instance
        );

        var recoveredCount = await service.RecoverStuckJobsAsync(CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var jobs = await dbContext.Jobs.ToDictionaryAsync(job => job.Id);

        recoveredCount.Should().Be(1);

        jobs[stuckJobId].Status.Should().Be(JobStatus.Pending);
        jobs[stuckJobId].RetryCount.Should().Be(1);
        jobs[stuckJobId].ProcessingStartedAtUtc.Should().BeNull();
        jobs[stuckJobId].NextRetryAtUtc.Should().Be(now.UtcDateTime.AddSeconds(30));

        jobs[recentProcessingJobId].RetryCount.Should().Be(0);
        jobs[recentProcessingJobId]
            .ProcessingStartedAtUtc.Should()
            .Be(now.UtcDateTime.AddSeconds(-30));
        jobs[recentProcessingJobId].NextRetryAtUtc.Should().BeNull();

        jobs[pendingJobId].RetryCount.Should().Be(0);
        jobs[pendingJobId].ProcessingStartedAtUtc.Should().Be(now.UtcDateTime.AddMinutes(-2));
        jobs[pendingJobId].NextRetryAtUtc.Should().BeNull();

        jobs[failedJobId].RetryCount.Should().Be(0);
        jobs[failedJobId].ProcessingStartedAtUtc.Should().Be(now.UtcDateTime.AddMinutes(-2));
        jobs[failedJobId].NextRetryAtUtc.Should().BeNull();
    }
}
