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
}
