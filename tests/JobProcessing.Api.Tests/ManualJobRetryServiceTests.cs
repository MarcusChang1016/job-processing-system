using FluentAssertions;
using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobProcessing.Api.Tests;

public class ManualJobRetryServiceTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _postgres;

    public ManualJobRetryServiceTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task RetryAsync_WhenJobDoesNotExist_ReturnsNotFound()
    {
        var now = new DateTimeOffset(2026, 08, 05, 23, 0, 0, TimeSpan.Zero);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        var service = new ManualJobRetryService(dbContext, new FixedTimeProvider(now));
        var result = await service.RetryAsync(Guid.NewGuid(), CancellationToken.None);

        result.Outcome.Should().Be(ManualJobRetryOutcome.NotFound);
        result.Job.Should().BeNull();
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.Success)]
    public async Task RetryAsync_WhenJobIsNotFailed_ReturnsInvalidStateAndDoesNotModifyJob(
        JobStatus status
    )
    {
        var now = new DateTimeOffset(2026, 08, 05, 23, 0, 0, TimeSpan.Zero);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        var service = new ManualJobRetryService(dbContext, new FixedTimeProvider(now));

        var jobId = Guid.NewGuid();
        var originalUpdatedAt = now.UtcDateTime.AddHours(-1);
        var originalRetryCount = 1;

        dbContext.Jobs.Add(
            new JobEntity
            {
                Id = jobId,
                Status = status,
                CreatedAtUtc = originalUpdatedAt.AddHours(-1),
                UpdatedAtUtc = originalUpdatedAt,
                RetryCount = originalRetryCount,
            }
        );

        await dbContext.SaveChangesAsync();

        var result = await service.RetryAsync(jobId, CancellationToken.None);

        result.Outcome.Should().Be(ManualJobRetryOutcome.InvalidState);
        result.Job.Should().BeNull();

        dbContext.ChangeTracker.Clear();

        var persistedJob = await dbContext.Jobs.FindAsync(jobId);

        persistedJob.Should().NotBeNull();
        persistedJob!.Status.Should().Be(status);
        persistedJob.UpdatedAtUtc.Should().Be(originalUpdatedAt);
        persistedJob.RetryCount.Should().Be(originalRetryCount);
    }

    [Fact]
    public async Task RetryAsync_WhenJobIsFailed_ResetsRetryStateAndPersistsChanges()
    {
        var now = new DateTimeOffset(2026, 08, 10, 22, 0, 0, TimeSpan.Zero);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        var service = new ManualJobRetryService(dbContext, new FixedTimeProvider(now));

        var jobId = Guid.NewGuid();
        var originalCreatedAt = now.UtcDateTime.AddHours(-1);

        dbContext.Jobs.Add(
            new JobEntity
            {
                Id = jobId,
                Status = JobStatus.Failed,
                CreatedAtUtc = originalCreatedAt,
                UpdatedAtUtc = now.UtcDateTime.AddHours(-1),
                CompletedAtUtc = now.UtcDateTime.AddHours(-1),
                ProcessingStartedAtUtc = now.UtcDateTime.AddHours(-1),
                NextRetryAtUtc = now.UtcDateTime.AddHours(1),
                RetryCount = 3,
                LastErrorMessage = "Original failure",
            }
        );

        await dbContext.SaveChangesAsync();

        var result = await service.RetryAsync(jobId, CancellationToken.None);

        result.Outcome.Should().Be(ManualJobRetryOutcome.Succeeded);
        result
            .Job.Should()
            .Be(
                new JobDetails(
                    Id: jobId,
                    Status: JobStatus.Pending,
                    CreatedAtUtc: originalCreatedAt,
                    UpdatedAtUtc: now.UtcDateTime,
                    RetryCount: 0,
                    CompletedAtUtc: null
                )
            );

        dbContext.ChangeTracker.Clear();

        var persistedJob = await dbContext.Jobs.FindAsync(jobId);

        persistedJob.Should().NotBeNull();
        persistedJob.Id.Should().Be(jobId);
        persistedJob.Status.Should().Be(JobStatus.Pending);
        persistedJob.CreatedAtUtc.Should().Be(originalCreatedAt);
        persistedJob.UpdatedAtUtc.Should().Be(now.UtcDateTime);
        persistedJob.CompletedAtUtc.Should().BeNull();
        persistedJob.ProcessingStartedAtUtc.Should().BeNull();
        persistedJob.NextRetryAtUtc.Should().BeNull();
        persistedJob.RetryCount.Should().Be(0);
        persistedJob.LastErrorMessage.Should().BeNull();
    }
}
