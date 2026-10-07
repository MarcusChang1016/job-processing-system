using System.Net;
using FluentAssertions;
using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Infrastructure.Weather;
using JobProcessing.Api.Options;
using JobProcessing.Api.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobProcessing.Api.Tests;

public class JobExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRequestIsCanceledButWorkerIsRunning_RetriesJob()
    {
        using var httpClient = new HttpClient(new CanceledRequestHandler());
        var service = CreateService(httpClient);
        var job = new JobEntity { Status = JobStatus.Processing };

        await service.ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Pending);
        job.RetryCount.Should().Be(1);
        job.NextRetryAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseIsServiceUnavailable_RetriesJob()
    {
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable)
        );
        var service = CreateService(httpClient);
        var job = new JobEntity { Status = JobStatus.Processing };

        await service.ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Pending);
        job.RetryCount.Should().Be(1);
        job.NextRetryAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenWorkerIsStopping_PropagatesCancellation()
    {
        using var httpClient = new HttpClient(new CanceledRequestHandler());
        var service = CreateService(httpClient);
        var job = new JobEntity { Status = JobStatus.Processing };
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> action = () => service.ExecuteAsync(job, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        job.Status.Should().Be(JobStatus.Processing);
        job.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBomReturnsValidObservation_CompletesJobWithWeatherResult()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK));
        var service = CreateService(httpClient);
        var job = new JobEntity { Status = JobStatus.Processing };

        await service.ExecuteAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Success);
        job.WeatherObservedAtUtc.Should().Be(new DateTime(2026, 9, 30, 20, 0, 0, DateTimeKind.Utc));
        job.AirTemperatureCelsius.Should().Be(13.8);
    }

    private static JobExecutionService CreateService(HttpClient httpClient)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new WorkerOptions { MaxRetryCount = 3, RetryCooldownSeconds = 30 }
        );
        var resultHandler = new JobExecutionResultHandler(new JobRetryPolicy(options));

        return new JobExecutionService(
            NullLogger<JobExecutionService>.Instance,
            resultHandler,
            TimeProvider.System,
            new BomWeatherClient(httpClient)
        );
    }

    private sealed class CanceledRequestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("BOM request timed out")
            );
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var response = new HttpResponseMessage(statusCode);
            if (statusCode is HttpStatusCode.OK)
            {
                string json = """
                    {
                        "observations":
                        {
                            "data":
                            [{
                                "aifstime_utc": "20260930200000",
                                "air_temp": 13.8
                            }]
                        }
                    }
                    """;
                response.Content = new StringContent(json);
            }

            return Task.FromResult(response);
        }
    }
}
