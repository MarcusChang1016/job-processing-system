namespace JobProcessing.Api.Jobs;

public enum ManualJobRetryOutcome
{
    Succeeded,
    NotFound,
    InvalidState,
}
