using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobProcessing.Api.Contracts;

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
}
