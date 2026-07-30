using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobProcessing.Api.Contracts;
using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Entities;
using Microsoft.AspNetCore.Mvc;
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

        await SeedJobAsync(
            factory,
            new JobEntity
            {
                Id = jobId,
                Status = JobStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                RetryCount = 0,
            }
        );

        var response = await client.PostAsync($"/jobs/{jobId}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Title.Should().Be("Invalid job state");
        problem.Detail.Should().Be("Only failed jobs can be retried.");
    }

    [Fact]
    public async Task RetryJob_WhenJobIsFailed_ReturnsRetriedJobAndClearsRetryState()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var jobId = Guid.NewGuid();

        await SeedJobAsync(
            factory,
            new JobEntity
            {
                Id = jobId,
                Status = JobStatus.Failed,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = DateTime.UtcNow,
                NextRetryAtUtc = DateTime.UtcNow.AddMinutes(5),
                RetryCount = 3,
                LastErrorMessage = "Original failure",
            }
        );

        var postResponse = await client.PostAsync($"/jobs/{jobId}/retry", content: null);

        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var job = await postResponse.Content.ReadFromJsonAsync<JobResponse>();

        job.Should().NotBeNull();
        job!.Id.Should().Be(jobId);
        job.Status.Should().Be("Pending");
        job.RetryCount.Should().Be(0);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persistedJob = await dbContext.Jobs.FindAsync(jobId);

            persistedJob.Should().NotBeNull();
            persistedJob!.Status.Should().Be(JobStatus.Pending);
            persistedJob.RetryCount.Should().Be(0);
            persistedJob.NextRetryAtUtc.Should().BeNull();
            persistedJob.CompletedAtUtc.Should().BeNull();
            persistedJob.ProcessingStartedAtUtc.Should().BeNull();
            persistedJob.LastErrorMessage.Should().BeNull();
        }
    }

    private static async Task SeedJobAsync(CustomWebApplicationFactory factory, JobEntity job)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();
    }
}
