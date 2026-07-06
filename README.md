# Job Processing System

A backend learning project built with ASP.NET Core, EF Core, SQLite, and a hosted background worker.

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
  -> EF Core / SQLite
  -> BackgroundService worker
  -> JobExecutionService
  -> JobExecutionResultHandler
  -> JobRetryPolicy when failure occurs
  -> update job status in database
```

This is currently a single-project modular monolith. It is not yet a full Clean Architecture solution, but it is structured so the project can evolve toward clearer application, infrastructure, and worker boundaries.

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

- Poll for pending jobs
- Recover stuck processing jobs
- Claim a job for processing
- Delegate execution to `JobExecutionService`
- Persist status changes

### Persistence

The project currently uses EF Core with SQLite.

Persistence responsibilities include:

- Storing job state
- Tracking retry count
- Tracking processing timestamps
- Tracking completion timestamps
- Supporting migrations
- Experimenting with optimistic concurrency through `RowVersion`

### Reliability

The system currently includes several reliability concepts:

- Retry policy through `JobRetryPolicy`
- Retry cooldown
- Maximum retry count
- Stuck job timeout detection
- Recovery from stale `Processing` state
- Fail-fast execution behaviour
- State transition validation
- Execution result handling through `JobExecutionResultHandler`
- Execution timestamps through `TimeProvider`

`JobRetryPolicy` owns the decision for what happens after a job execution failure. It decides whether the job should return to `Pending` for retry or move to `Failed` after reaching the maximum retry count.

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

- .NET 9
- ASP.NET Core Web API
- BackgroundService
- EF Core
- SQLite
- Swagger / OpenAPI
- Health Checks
- Structured logging
- xUnit
- FluentAssertions
- TimeProvider

## Learning Roadmap

The project will continue to grow in stages.

Planned next areas:

- Broader unit test coverage
- Integration tests
- Testcontainers
- Better separation between API, application logic, infrastructure, and worker concerns
- Docker and Docker Compose
- GitHub Actions CI
- PostgreSQL
- JWT authentication and authorisation
- OpenTelemetry, Prometheus, and Grafana
- Cloud deployment

## Current Limitations

This project is still evolving. Some known limitations are:

- API controllers currently access `AppDbContext` directly.
- API and worker currently run in the same project and process.
- `JobWorker` contains several responsibilities that may later be extracted.
- Test coverage is still early and currently focuses on state transitions, DTO mapping, retry policy, and execution result handling.
- Docker, CI/CD, authentication, and production observability are not implemented yet.

These limitations are intentional learning opportunities and will guide future refactoring.

## Documentation

More detailed design notes live in the `docs` folder.

- `docs/architecture.md` describes the system architecture and component responsibilities.
- `docs/system_flow.md` describes the high-level job processing flow.
