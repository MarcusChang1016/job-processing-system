using System.Text.Json;
using FluentAssertions;
using JobProcessing.Api.Infrastructure.Weather;

namespace JobProcessing.Api.Tests;

public class BomObservationResponseTests
{
    [Fact]
    public void Deserialize_ReturnsLatestObservationFields()
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

        var result = JsonSerializer.Deserialize<BomObservationResponse>(json);

        result.Should().NotBeNull();

        var latestObservation = result!.Observations.Data[0];
        latestObservation.ObservationTimeUtcText.Should().Be("20260930200000");
        latestObservation.AirTemperatureCelsius.Should().Be(13.8);
    }
}
