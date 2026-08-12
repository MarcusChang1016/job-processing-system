namespace JobProcessing.Api.Application.Jobs;

public enum ManualJobRetryOutcome
{
    Succeeded,
    NotFound,
    InvalidState,
}
