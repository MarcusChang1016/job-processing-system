using Microsoft.EntityFrameworkCore;
using JobProcessing.Api.Infrastructure.Entities;

namespace JobProcessing.Api.Infrastructure;

public class AppDbContext : DbContext
{
    public DbSet<JobEntity> Jobs => Set<JobEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
}