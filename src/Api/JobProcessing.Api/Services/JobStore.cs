using JobProcessing.Api.Models;

namespace JobProcessing.Api.Services;

public class JobStore
{
    private readonly List<Job> _jobs = new();

    public void AddJob(Job job) => _jobs.Add(job);

    public Job? GetJob(Guid id)
    {
        return _jobs.FirstOrDefault(j => j.Id == id);
    }

    public List<Job> GetPendingJobs()
    {
        return _jobs.Where(j => j.Status == "Pending").ToList();
    }

    public void UpdateJob(Job job)
    {
        var existingJob = GetJob(job.Id);

        if (existingJob != null)
        {
            existingJob.Status = job.Status;
            existingJob.UpdatedAt = job.UpdatedAt;
            existingJob.RetryCount = job.RetryCount;
        }
    }
}