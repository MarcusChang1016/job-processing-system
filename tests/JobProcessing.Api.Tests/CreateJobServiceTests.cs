using FluentAssertions;
using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobProcessing.Api.Tests;

public class CreateJobServiceTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _postgres;

    public CreateJobServiceTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnJobWithInitialState()
    {
        var now = new DateTimeOffset(2026, 08, 20, 10, 0, 0, TimeSpan.Zero);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        var service = new CreateJobService(dbContext, new FixedTimeProvider(now));

        var result = await service.CreateAsync(CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        result.Status.Should().Be(JobStatus.Pending);
        result.RetryCount.Should().Be(0);
        result.CreatedAtUtc.Should().Be(now.UtcDateTime);
        result.UpdatedAtUtc.Should().Be(now.UtcDateTime);
        result.CompletedAtUtc.Should().BeNull();

        dbContext.ChangeTracker.Clear();

        var persistedJob = await dbContext.Jobs.FindAsync(result.Id);

        persistedJob.Should().NotBeNull();
        persistedJob!.Status.Should().Be(JobStatus.Pending);
        persistedJob.RetryCount.Should().Be(0);
        persistedJob.CreatedAtUtc.Should().Be(now.UtcDateTime);
        persistedJob.UpdatedAtUtc.Should().Be(now.UtcDateTime);
    }
}
