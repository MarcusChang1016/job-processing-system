namespace JobProcessing.Api.Models;

public record Job
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; } = 0;
}