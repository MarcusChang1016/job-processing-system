using FluentAssertions;
using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Options;
using JobProcessing.Api.Tests.Infrastructure;
using JobProcessing.Api.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace JobProcessing.Api.Tests;

public class JobClaimServiceTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _postgres;

    public JobClaimServiceTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task ClaimNextJobAsync_ShouldClaimOldestEligiblePendingJob()
    {
        var now = new DateTimeOffset(2026, 07, 09, 23, 0, 0, TimeSpan.Zero);

        var workerOptions = OptionsFactory.Create(
            new WorkerOptions
            {
                MaxRetryCount = 3,
                RetryCooldownSeconds = 30,
                StuckJobTimeoutSeconds = 60,
            }
        );

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();
        await dbContext.Jobs.ExecuteDeleteAsync();

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

    [Fact]
    public async Task ClaimNextJobAsync_WhenTrackedJobWasUpdatedByAnotherContext_ReturnsNull()
    {
        var now = new DateTimeOffset(2026, 07, 09, 23, 0, 0, TimeSpan.Zero);
        var competingUpdateTime = now.UtcDateTime.AddMinutes(-1);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        var jobId = Guid.NewGuid();
        await using (var setupContext = new AppDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            await setupContext.Jobs.ExecuteDeleteAsync();
            setupContext.Jobs.Add(
                new JobEntity
                {
                    Id = jobId,
                    Status = JobStatus.Pending,
                    CreatedAtUtc = now.UtcDateTime.AddMinutes(-10),
                    UpdatedAtUtc = now.UtcDateTime.AddMinutes(-10),
                }
            );
            await setupContext.SaveChangesAsync();
        }

        await using var staleContext = new AppDbContext(options);
        await staleContext.Jobs.SingleAsync(job => job.Id == jobId);

        await using (var competingContext = new AppDbContext(options))
        {
            var competingJob = await competingContext.Jobs.SingleAsync(job => job.Id == jobId);
            competingJob.UpdatedAtUtc = competingUpdateTime;
            await competingContext.SaveChangesAsync();
        }

        var workerOptions = OptionsFactory.Create(new WorkerOptions { MaxRetryCount = 3 });
        var service = new JobClaimService(
            staleContext,
            workerOptions,
            new FixedTimeProvider(now),
            NullLogger<JobClaimService>.Instance
        );

        var claimedJob = await service.ClaimNextJobAsync(CancellationToken.None);

        claimedJob.Should().BeNull();

        await using var verificationContext = new AppDbContext(options);
        var persistedJob = await verificationContext.Jobs.SingleAsync(job => job.Id == jobId);
        persistedJob.Status.Should().Be(JobStatus.Pending);
        persistedJob.UpdatedAtUtc.Should().Be(competingUpdateTime);
        persistedJob.ProcessingStartedAtUtc.Should().BeNull();
    }
}
