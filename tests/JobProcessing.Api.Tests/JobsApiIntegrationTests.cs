using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobProcessing.Api.Contracts;
using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace JobProcessing.Api.Tests;

public class JobsApiIntegrationTests
{
    [Fact]
    public async Task CreateJob_ReturnsCreatedJob()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/jobs", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var job = await response.Content.ReadFromJsonAsync<JobResponse>();

        job.Should().NotBeNull();
        job!.Id.Should().NotBeEmpty();
        job.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task CreateJob_ReturnsLocationThatCanBeUsedToGetJob()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsync("/jobs", content: null);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var getResponse = await client.GetAsync(createResponse.Headers.Location);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createdJob = await createResponse.Content.ReadFromJsonAsync<JobResponse>();
        var fetchedJob = await getResponse.Content.ReadFromJsonAsync<JobResponse>();

        createdJob.Should().NotBeNull();
        fetchedJob.Should().NotBeNull();
        fetchedJob.Id.Should().Be(createdJob.Id);
        fetchedJob.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task GetJob_WhenJobDoesNotExist_ReturnsNotFound()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RetryJob_WhenJobDoesNotExist_ReturnsNotFound()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/jobs/{Guid.NewGuid()}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RetryJob_WhenJobIsNotFailed_ReturnsBadRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var jobId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Jobs.Add(
                new JobEntity
                {
                    Id = jobId,
                    Status = JobStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    RetryCount = 0,
                }
            );

            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsync($"/jobs/{jobId}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
