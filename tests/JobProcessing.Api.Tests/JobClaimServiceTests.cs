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

public class JobClaimServiceTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task ClaimNextJobAsync_ShouldClaimOldestEligiblePendingJob()
    {
        var now = new DateTimeOffset(2026, 07, 09, 23, 0, 0, TimeSpan.Zero);

        var workerOptions = Options.Create(
            new WorkerOptions
            {
                MaxRetryCount = 3,
                RetryCooldownSeconds = 30,
                StuckJobTimeoutSeconds = 60,
            }
        );

        // Build test-only database
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        await using var dbContext = new AppDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();

        var oldestEligibleJobId = Guid.NewGuid();
        var newerEligibleJobId = Guid.NewGuid();
        var futureRetryJobId = Guid.NewGuid();
        var maxRetryJobId = Guid.NewGuid();

        dbContext.Jobs.AddRange(
            new JobEntity
            {
                Id = oldestEligibleJobId,
                Status = JobStatus.Pending,
                RetryCount = 0,
                CreatedAtUtc = now.UtcDateTime.AddMinutes(-10),
                UpdatedAtUtc = now.UtcDateTime.AddMinutes(-10),
                NextRetryAtUtc = null,
            },
            new JobEntity
            {
                Id = newerEligibleJobId,
                Status = JobStatus.Pending,
                RetryCount = 0,
                CreatedAtUtc = now.UtcDateTime.AddMinutes(-5),
                UpdatedAtUtc = now.UtcDateTime.AddMinutes(-5),
                NextRetryAtUtc = null,
            },
            new JobEntity
            {
                Id = futureRetryJobId,
                Status = JobStatus.Pending,
                RetryCount = 0,
                CreatedAtUtc = now.UtcDateTime.AddMinutes(-20),
                UpdatedAtUtc = now.UtcDateTime.AddMinutes(-20),
                NextRetryAtUtc = now.UtcDateTime.AddMinutes(5),
            },
            new JobEntity
            {
                Id = maxRetryJobId,
                Status = JobStatus.Pending,
                RetryCount = 3,
                CreatedAtUtc = now.UtcDateTime.AddMinutes(-30),
                UpdatedAtUtc = now.UtcDateTime.AddMinutes(-30),
                NextRetryAtUtc = null,
            }
        );

        await dbContext.SaveChangesAsync();

        var service = new JobClaimService(
            dbContext,
            workerOptions,
            new FixedTimeProvider(now),
            NullLogger<JobClaimService>.Instance
        );

        var claimedJob = await service.ClaimNextJobAsync(CancellationToken.None);

        claimedJob.Should().NotBeNull();
        claimedJob!.Id.Should().Be(oldestEligibleJobId);
        claimedJob.Status.Should().Be(JobStatus.Processing);
        claimedJob.ProcessingStartedAtUtc.Should().Be(now.UtcDateTime);
        claimedJob.UpdatedAtUtc.Should().Be(now.UtcDateTime);

        dbContext.ChangeTracker.Clear();

        var jobs = await dbContext.Jobs.ToDictionaryAsync(job => job.Id);

        jobs[oldestEligibleJobId].Status.Should().Be(JobStatus.Processing);
        jobs[oldestEligibleJobId].ProcessingStartedAtUtc.Should().Be(now.UtcDateTime);
        jobs[oldestEligibleJobId].UpdatedAtUtc.Should().Be(now.UtcDateTime);

        jobs[newerEligibleJobId].Status.Should().Be(JobStatus.Pending);
        jobs[newerEligibleJobId].ProcessingStartedAtUtc.Should().BeNull();
        jobs[futureRetryJobId].Status.Should().Be(JobStatus.Pending);
        jobs[maxRetryJobId].Status.Should().Be(JobStatus.Pending);
    }
}
