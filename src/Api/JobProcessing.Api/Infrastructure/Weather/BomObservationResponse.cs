using System.Text.Json.Serialization;

namespace JobProcessing.Api.Infrastructure.Weather;

public sealed class BomObservationResponse
{
    [JsonPropertyName("observations")]
    public required BomObservations Observations { get; set; }
}

public sealed class BomObservations
{
    [JsonPropertyName("data")]
    public IReadOnlyList<BomObservation> Data { get; set; } = [];
}

public sealed class BomObservation
{
    [JsonPropertyName("aifstime_utc")]
    public string ObservationTimeUtcText { get; set; } = string.Empty;

    [JsonPropertyName("air_temp")]
    public double? AirTemperatureCelsius { get; set; }
}
