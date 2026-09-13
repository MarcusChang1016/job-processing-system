using FluentAssertions;
using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Tests.Infrastructure;

namespace JobProcessing.Api.Tests;

public class GetJobServiceTests
{
    [Fact]
    public async Task GetAsync_WhenJobDoesNotExist_ReturnsNull()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = new GetJobService(database.DbContext);

        var result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenJobExists_ReturnsJobDetails()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Failed,
            CreatedAtUtc = new(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc),
            RetryCount = 3,
            CompletedAtUtc = null,
        };

        database.DbContext.Jobs.Add(job);
        await database.DbContext.SaveChangesAsync();
        database.DbContext.ChangeTracker.Clear();

        var service = new GetJobService(database.DbContext);

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
}
