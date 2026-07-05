using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure.Entities;
using JobProcessing.Api.Models;
using Microsoft.Extensions.Options;

namespace JobProcessing.Api.Services;

public class JobRetryPolicy
{
    private readonly WorkerOptions _workerOptions;

    public JobRetryPolicy(IOptions<WorkerOptions> options)
    {
        _workerOptions = options.Value;
    }

    public void ApplyFailure(JobEntity job, string errorMessage, DateTime now)
    {
        job.RetryCount += 1;
        job.UpdatedAtUtc = now;
        job.LastErrorMessage = errorMessage;

        if (job.RetryCount < _workerOptions.MaxRetryCount)
        {
            job.Status = JobStatus.Pending;
            job.NextRetryAtUtc = now.AddSeconds(_workerOptions.RetryCooldownSeconds);
        }
        else
        {
            job.Status = JobStatus.Failed;
            job.NextRetryAtUtc = null;
        }
    }
}
