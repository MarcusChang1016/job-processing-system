using JobProcessing.Api.Application.Jobs;
using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Infrastructure.Weather;
using JobProcessing.Api.Options;
using JobProcessing.Api.Worker;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddHostedService<JobWorker>();

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddHttpClient<BomWeatherClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<JobRetryPolicy>();
builder.Services.AddScoped<JobExecutionResultHandler>();
builder.Services.AddScoped<JobExecutionService>();
builder.Services.AddScoped<JobRecoveryService>();
builder.Services.AddScoped<JobClaimService>();
builder.Services.AddScoped<GetJobService>();
builder.Services.AddScoped<JobProcessor>();
builder.Services.AddScoped<CreateJobService>();
builder.Services.AddScoped<ManualJobRetryService>();

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("WorkerOptions"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(
    (serviceProvider, options) =>
    {
        var connectionString =
            serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("JobProcessing")
            ?? throw new InvalidOperationException(
                "ConnectionStrings: JobProcessing is not configured"
            );

        options.UseNpgsql(connectionString);
    }
);

builder.Services.AddHealthChecks();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program; // For integration testing
