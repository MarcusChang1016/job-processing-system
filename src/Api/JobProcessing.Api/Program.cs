using JobProcessing.Api.Infrastructure;
using JobProcessing.Api.Models;
using JobProcessing.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddHostedService<JobWorker>();

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddScoped<JobRetryPolicy>();
builder.Services.AddScoped<JobExecutionResultHandler>();
builder.Services.AddScoped<JobExecutionService>();
builder.Services.AddScoped<JobRecoveryService>();
builder.Services.AddScoped<JobClaimService>();
builder.Services.AddScoped<JobProcessor>();

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("WorkerOptions"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=jobs.db"));

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
