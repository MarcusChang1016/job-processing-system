using FluentAssertions;
using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobProcessing.Api.Tests;

public class GetJobServiceTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _postgres;

    public GetJobServiceTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task GetAsync_WhenJobDoesNotExist_ReturnsNull()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        var service = new GetJobService(dbContext);

        var result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenJobExists_ReturnsJobDetails()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Failed,
            CreatedAtUtc = new(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc),
            RetryCount = 3,
            CompletedAtUtc = null,
        };

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var service = new GetJobService(dbContext);

        var result = await service.GetAsync(job.Id, CancellationToken.None);

        result
            .Should()
            .Be(
                new JobDetails(
                    job.Id,
                    JobStatus.Failed,
                    job.CreatedAtUtc,
                    job.UpdatedAtUtc,
                    3,
                    null
                )
            );
    }

    [Fact]
    public async Task GetAsync_WhenWeatherJobSucceeded_ReturnsObservation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Success,
            CreatedAtUtc = new(2026, 10, 07, 23, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new(2026, 10, 07, 23, 30, 0, DateTimeKind.Utc),
            RetryCount = 0,
            CompletedAtUtc = new(2026, 10, 07, 23, 30, 0, DateTimeKind.Utc),
            WeatherObservedAtUtc = new(2026, 10, 07, 20, 30, 0, DateTimeKind.Utc),
            AirTemperatureCelsius = 13.8,
        };

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var service = new GetJobService(dbContext);

        var result = await service.GetAsync(job.Id, CancellationToken.None);

        result
            .Should()
            .Be(
                new JobDetails(
                    job.Id,
                    JobStatus.Success,
                    job.CreatedAtUtc,
                    job.UpdatedAtUtc,
                    0,
                    job.CompletedAtUtc,
                    job.WeatherObservedAtUtc,
                    job.AirTemperatureCelsius
                )
            );
    }
}
