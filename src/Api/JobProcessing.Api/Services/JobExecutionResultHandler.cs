using JobProcessing.Api.Enums;
using JobProcessing.Api.Infrastructure.Entities;

namespace JobProcessing.Api.Services;

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
    }

    public void ApplyFailure(JobEntity job, string errorMessage, DateTime now)
    {
        _jobRetryPolicy.ApplyFailure(job, errorMessage, now);
    }
}
