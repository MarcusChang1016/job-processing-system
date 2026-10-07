using System.Globalization;
using JobProcessing.Api.Application.Weather;

namespace JobProcessing.Api.Infrastructure.Weather;

public static class BomObservationMapper
{
    public static WeatherObservation MapLatest(BomObservationResponse response)
    {
        if (response.Observations.Data.Count == 0)
        {
            throw new InvalidDataException("BOM response does not contain any observations.");
        }

        var target = response.Observations.Data[0];

        var dateTime = DateTime.ParseExact(
            target.ObservationTimeUtcText,
            "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );

        var temperature =
            target.AirTemperatureCelsius
            ?? throw new InvalidDataException(
                "Latest BOM observation does not contain an air temperature."
            );

        return new WeatherObservation(dateTime, temperature);
    }
}
