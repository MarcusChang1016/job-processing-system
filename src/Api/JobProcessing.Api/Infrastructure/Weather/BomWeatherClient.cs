using System.Net.Http.Json;
using JobProcessing.Api.Application.Weather;

namespace JobProcessing.Api.Infrastructure.Weather;

public sealed class BomWeatherClient
{
    private readonly HttpClient _httpClient;

    public BomWeatherClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WeatherObservation> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            "https://www.bom.gov.au/fwo/IDQ60901/IDQ60901.94576.json",
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var bomResponse =
            await response.Content.ReadFromJsonAsync<BomObservationResponse>(cancellationToken)
            ?? throw new InvalidDataException("BOM response body was empty");

        return BomObservationMapper.MapLatest(bomResponse);
    }
}
