using FluentAssertions;
using JobProcessing.Api.Infrastructure.Weather;

namespace JobProcessing.Api.Tests;

public class BomObservationMapperTests
{
    [Fact]
    public void MapLatest_ReturnsParsedObservation()
    {
        var response = new BomObservationResponse
        {
            Observations = new BomObservations
            {
                Data = new List<BomObservation>
                {
                    new()
                    {
                        ObservationTimeUtcText = "20260930200000",
                        AirTemperatureCelsius = 13.8,
                    },
                },
            },
        };

        var result = BomObservationMapper.MapLatest(response);

        result.Should().NotBeNull();

        result.ObservedAtUtc.Should().Be(new DateTime(2026, 09, 30, 20, 0, 0, 0, DateTimeKind.Utc));
        result.AirTemperatureCelsius.Should().Be(13.8);
    }

    [Fact]
    public void MapLatest_WhenTemperatureIsMissing_ThrowsInvalidDataException()
    {
        var response = new BomObservationResponse
        {
            Observations = new BomObservations
            {
                Data = new List<BomObservation>
                {
                    new()
                    {
                        ObservationTimeUtcText = "20260930200000",
                        AirTemperatureCelsius = null,
                    },
                },
            },
        };

        Action action = () => BomObservationMapper.MapLatest(response);

        action
            .Should()
            .Throw<InvalidDataException>()
            .WithMessage("Latest BOM observation does not contain an air temperature.");
    }

    [Fact]
    public void MapLatest_WhenNoObservations_ThrowsInvalidDataException()
    {
        var response = new BomObservationResponse
        {
            Observations = new BomObservations { Data = [] },
        };

        Action action = () => BomObservationMapper.MapLatest(response);

        action
            .Should()
            .Throw<InvalidDataException>()
            .WithMessage("BOM response does not contain any observations.");
    }
}
