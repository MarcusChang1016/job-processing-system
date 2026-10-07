using System.Net;
using FluentAssertions;
using JobProcessing.Api.Infrastructure.Weather;

namespace JobProcessing.Api.Tests;

public class BomWeatherClientTests
{
    [Fact]
    public async Task GetLatestAsync_WhenResponseIsValid_ReturnsWeatherObservation()
    {
        // Given
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
        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json));

        // When
        var client = new BomWeatherClient(httpClient);
        var result = await client.GetLatestAsync(CancellationToken.None);

        // Then
        result.ObservedAtUtc.Should().Be(new DateTime(2026, 9, 30, 20, 0, 0, DateTimeKind.Utc));
        result.AirTemperatureCelsius.Should().Be(13.8);
    }

    [Fact]
    public async Task GetLatestAsync_WhenBomReturnsServiceUnavailable_ThrowsHttpRequestException()
    {
        // Given
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
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, json)
        );

        // When
        var client = new BomWeatherClient(httpClient);
        Func<Task> action = () => client.GetLatestAsync(CancellationToken.None);

        // Then
        var exception = await action.Should().ThrowAsync<HttpRequestException>();

        exception.Which.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string json)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json),
            };

            return Task.FromResult(response);
        }
    }
}
