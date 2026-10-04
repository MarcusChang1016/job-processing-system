# Job Processing System

A backend learning project built with ASP.NET Core, EF Core, PostgreSQL, and a hosted background worker.

The goal of this project is to practice and demonstrate backend engineering concepts through a small but realistic job processing system. It is intentionally being built step by step, so the codebase can show both working features and future architectural improvements.

## Project Goals

This project is designed to help me learn, practice, and demonstrate:

- ASP.NET Core Web API design
- Background processing with `BackgroundService`
- Dependency Injection and scoped services
- EF Core persistence and migrations
- Retry behaviour and failure handling
- Job state transitions
- Basic observability with logging, health checks, and metrics
- Testing, Docker, CI/CD, and cloud deployment in later stages

The project is also intended to become a portfolio project that shows backend engineering judgment, not just framework usage.

## Current Architecture

The current system runs the API and worker in the same ASP.NET Core process.

```text
Client
  -> ASP.NET Core API
       -> Create endpoint -> CreateJobService -> AppDbContext
       -> Get endpoint -> GetJobService -> AppDbContext
       -> Retry endpoint -> ManualJobRetryService -> AppDbContext

BackgroundService worker
  -> JobRecoveryService for stale Processing jobs
  -> JobClaimService for eligible Pending jobs
  -> JobProcessor for claimed job processing
  -> JobExecutionService
  -> JobExecutionResultHandler
  -> JobRetryPolicy for failed attempts

AppDbContext -> EF Core / PostgreSQL
```

This is currently a single-project modular monolith. It is not yet a full Clean Architecture solution, but it is structured so the project can evolve toward clearer application, infrastructure, and worker boundaries.

Current source organisation:

```text
src/Api/JobProcessing.Api
  Api/
    Controllers/
    Contracts/
  Application/
    Jobs/
  Domain/
    Jobs/
  Infrastructure/
    Entities/
    Migrations/
  Options/
  Worker/
```

## Main Components

### API

The API exposes endpoints for creating, reading, retrying, and observing jobs.

- `POST /jobs` creates a new job with `Pending` status.
- `GET /jobs/{id}` returns the current job state.
- `POST /jobs/{id}/retry` retries a failed job.
- `GET /metrics` returns basic job counts by status.
- `GET /health` exposes a health check endpoint.

### Background Worker

The worker runs continuously in the background using `BackgroundService`.

Its responsibilities are:

- Run the polling loop
- Create scoped services per polling cycle
- Delegate stuck processing job recovery to `JobRecoveryService`
- Delegate eligible pending job claiming to `JobClaimService`
- Delegate claimed job processing to `JobProcessor`

### Persistence

The project uses EF Core with PostgreSQL. Local development runs PostgreSQL through Docker Compose, while database-backed tests use disposable PostgreSQL containers through Testcontainers.

Persistence responsibilities include:

- Storing job state
- Tracking retry count
- Tracking processing timestamps
- Tracking completion timestamps
- Supporting migrations
- Detecting optimistic concurrency conflicts through PostgreSQL's `xmin` system column
- Recreating a clean database from PostgreSQL-specific EF Core migrations

### Reliability

The system currently includes several reliability concepts:

- Retry policy through `JobRetryPolicy`
- Retry cooldown
- Maximum retry count
- Stuck job timeout detection
- Recovery from stale `Processing` state
- Atomic eligible job claiming through PostgreSQL row locking
- Locked-row skipping for multiple worker compatibility
- Fail-fast execution behaviour
- State transition validation
- Claimed job processing through `JobProcessor`
- Execution result handling through `JobExecutionResultHandler`
- Execution timestamps through `TimeProvider`
- Explicit failed-job retry through `ManualJobRetryService`

`JobRetryPolicy` owns the decision for what happens after a failed job attempt. A failed attempt can come from execution failure or stuck job recovery. It decides whether the job should return to `Pending` with retry cooldown or move to `Failed` after reaching the maximum retry count.

`ManualJobRetryService` owns the separate client-initiated retry use case. It only accepts jobs already in `Failed`, returns them to `Pending`, resets their automatic retry budget, and clears previous execution state. `JobsController` maps the service outcome to the HTTP response without owning those business rules.

`GetJobService` owns the job lookup use case. It performs a no-tracking database query and returns an application-level `JobDetails` result, so `JobsController` does not depend on EF Core entities or persistence details.

These are intentionally implemented in a simple form first, so they can be tested and improved later.

## Job Lifecycle

```text
Pending
  -> Processing
  -> Success

Processing
  -> Pending   retry after failure
  -> Failed    max retry reached

Failed
  -> Pending   manual retry
```

## Current Tech Stack

- .NET 10
- ASP.NET Core Web API
- BackgroundService
- EF Core
- PostgreSQL
- Docker Compose for the local database
- Testcontainers for database-backed tests
- Swagger / OpenAPI
- Health Checks
- Structured logging
- xUnit
- FluentAssertions
- TimeProvider

## Local Development

Docker is required for both the local PostgreSQL database and database-backed tests.

Create the local Compose environment file, set a development-only password, and start PostgreSQL:

```bash
cp .env.example .env
docker compose up -d
docker compose ps
```

The `.env` file configures Docker Compose; ASP.NET Core does not load it automatically. Before starting the API, expose a connection string that uses the same password:

```bash
read -rsp "PostgreSQL password: " JOB_PROCESSING_DB_PASSWORD && echo
export ConnectionStrings__JobProcessing="Host=localhost;Port=5432;Database=job_processing;Username=job_processing;Password=${JOB_PROCESSING_DB_PASSWORD}"
unset JOB_PROCESSING_DB_PASSWORD

dotnet run --project src/Api/JobProcessing.Api/JobProcessing.Api.csproj
```

When the API is stopped, remove the connection string from the current shell:

```bash
unset ConnectionStrings__JobProcessing
```

Run the test suite while Docker is available:

```bash
dotnet test tests/JobProcessing.Api.Tests/JobProcessing.Api.Tests.csproj
```

Database-backed tests create disposable PostgreSQL containers with Testcontainers; they do not use the local `job_processing` database.

## Roadmap

The next milestones build on the existing job lifecycle:

1. Replace simulated execution with one deterministic job type, including success,
   failure, retry, and cancellation tests.
2. Containerise the application and add CI build and test checks.
3. Improve health signals, logging, and job metrics so failures can be diagnosed.
4. Deploy the system and demonstrate recovery after an interrupted job.

Claim-query indexing and higher-load testing will follow measured demand. Additional
infrastructure or project boundaries will be introduced when they solve a concrete
problem in the running system.

## Current Limitations

This project is still evolving. Some known limitations are:

- API and worker currently run in the same project and process, with folders separating API, application use cases, domain job rules, infrastructure, options, and worker pipeline code.
- `JobWorker` still orchestrates the polling loop and scoped worker services; recovery, claiming, and processing are handled by dedicated services.
- `JobExecutionService` still simulates work with a delay and random success or failure; no real job contract exists yet.
- Test coverage currently includes state transitions, DTO mapping, retry policy, execution result handling, stuck job recovery, atomic job claiming and locked-row skipping, database-backed create, read, and manual retry service tests, and Jobs API integration tests.
- The application itself is not containerised yet; Docker Compose currently provides PostgreSQL only.
- CI/CD, authentication, and production observability are not implemented yet.

These limitations define the current scope and the next engineering improvements.

## Documentation

More detailed design notes live in the `docs` folder.

- `docs/architecture.md` describes the system architecture and component responsibilities.
- `docs/system_flow.md` describes the high-level job processing flow.
- `docs/decisions.md` records significant engineering decisions and their trade-offs.
