using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Infrastructure.Weather;

namespace JobProcessing.Api.Worker;

public class JobExecutionService
{
    private readonly ILogger<JobExecutionService> _logger;
    private readonly JobExecutionResultHandler _jobExecutionResultHandler;
    private readonly TimeProvider _timeProvider;
    private readonly BomWeatherClient _bomWeatherClient;

    public JobExecutionService(
        ILogger<JobExecutionService> logger,
        JobExecutionResultHandler jobExecutionResultHandler,
        TimeProvider timeProvider,
        BomWeatherClient bomWeatherClient
    )
    {
        _logger = logger;
        _jobExecutionResultHandler = jobExecutionResultHandler;
        _timeProvider = timeProvider;
        _bomWeatherClient = bomWeatherClient;
    }

    public async Task ExecuteAsync(JobEntity job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing job {JobId}", job.Id);

        var startAt = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            var observation = await _bomWeatherClient.GetLatestAsync(stoppingToken);

            _logger.LogInformation(
                "Fetched BOM observation at {ObservedAtUtc}: {AirTemperatureCelsius} C",
                observation.ObservedAtUtc,
                observation.AirTemperatureCelsius
            );

            var finishedAt = _timeProvider.GetUtcNow().UtcDateTime;

            _jobExecutionResultHandler.ApplySuccess(job, observation, finishedAt);

            _logger.LogInformation("Job {id} completed successfully", job.Id);
            _logger.LogInformation(
                "JobResult {@JobResult}",
                new JobResult
                {
                    JobId = job.Id,
                    Success = true,
                    RetryCount = job.RetryCount,
                    StartedAtUtc = startAt,
                    FinishedAtUtc = finishedAt,
                }
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            var finishedAt = _timeProvider.GetUtcNow().UtcDateTime;

            _jobExecutionResultHandler.ApplyFailure(job, ex.Message, finishedAt);

            _logger.LogInformation(
                "Job result: {@JobResult}",
                new JobResult
                {
                    JobId = job.Id,
                    Success = false,
                    RetryCount = job.RetryCount,
                    StartedAtUtc = startAt,
                    FinishedAtUtc = finishedAt,
                    ErrorMessage = ex.Message,
                }
            );
        }
    }
}
