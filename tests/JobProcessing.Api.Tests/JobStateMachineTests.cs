using FluentAssertions;
using JobProcessing.Api.Enums;
using JobProcessing.Api.Services;

namespace JobProcessing.Api.Tests;

public class JobStateMachineTests
{
    [Theory]
    [InlineData(JobStatus.Pending, JobStatus.Processing)]
    [InlineData(JobStatus.Processing, JobStatus.Success)]
    [InlineData(JobStatus.Processing, JobStatus.Failed)]
    [InlineData(JobStatus.Processing, JobStatus.Pending)]
    [InlineData(JobStatus.Failed, JobStatus.Pending)]
    public void CanTransition_ShouldReturnTrue_WhenTransitionIsAllowed(
        JobStatus current,
        JobStatus next
    )
    {
        var result = JobStateMachine.CanTransition(current, next);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(JobStatus.Pending, JobStatus.Success)]
    [InlineData(JobStatus.Pending, JobStatus.Failed)]
    [InlineData(JobStatus.Failed, JobStatus.Success)]
    [InlineData(JobStatus.Failed, JobStatus.Failed)]
    [InlineData(JobStatus.Success, JobStatus.Pending)]
    [InlineData(JobStatus.Success, JobStatus.Processing)]
    [InlineData(JobStatus.Success, JobStatus.Failed)]
    public void CanTransition_ShouldReturnFalse_WhenTransitionIsNotAllowed(
        JobStatus current,
        JobStatus next
    )
    {
        var result = JobStateMachine.CanTransition(current, next);

        result.Should().BeFalse();
    }
}
