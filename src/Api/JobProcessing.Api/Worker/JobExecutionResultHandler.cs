using JobProcessing.Api.Domain.Jobs;
using JobProcessing.Api.Infrastructure.Entities;

namespace JobProcessing.Api.Worker;

public class JobExecutionResultHandler
{
    private readonly JobRetryPolicy _jobRetryPolicy;

    public JobExecutionResultHandler(JobRetryPolicy jobRetryPolicy)
    {
        _jobRetryPolicy = jobRetryPolicy;
    }

    public void ApplySuccess(JobEntity job, DateTime now)
    {
        job.Status = JobStatus.Success;
        job.UpdatedAtUtc = now;
        job.CompletedAtUtc = now;
        job.LastErrorMessage = null;
        job.NextRetryAtUtc = null;
        job.ProcessingStartedAtUtc = null;
    }

    public void ApplyFailure(JobEntity job, string errorMessage, DateTime now)
    {
        _jobRetryPolicy.ApplyFailedAttempt(job, errorMessage, now);
    }
}
