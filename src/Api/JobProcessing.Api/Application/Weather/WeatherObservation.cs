namespace JobProcessing.Api.Application.Weather;

public sealed record WeatherObservation(DateTime ObservedAtUtc, double AirTemperatureCelsius);
