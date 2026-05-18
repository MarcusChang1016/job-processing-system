using JobProcessing.Api.Enums;

namespace JobProcessing.Api.Services;

public static class JobStateMachine
{
    public static bool CanTransition(JobStatus current, JobStatus next)
    {
        return current switch
        {
            JobStatus.Pending => next == JobStatus.Processing,

            JobStatus.Processing => next == JobStatus.Success
                || next == JobStatus.Failed
                || next == JobStatus.Pending,

            JobStatus.Failed => next == JobStatus.Pending,

            JobStatus.Success => false,

            _ => false,
        };
    }
}
