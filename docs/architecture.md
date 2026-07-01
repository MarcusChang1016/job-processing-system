# Job Processing System - Architecture

## Overview

The current system is an ASP.NET Core application that hosts both a Web API and a background worker in the same process.

The API is responsible for accepting client requests and exposing job state. The worker is responsible for polling persisted jobs, executing them, handling retries, and recovering stale processing work.

The current architecture is intentionally simple:

```text
Client
  -> ASP.NET Core API
  -> EF Core / SQLite
  -> BackgroundService worker
  -> JobExecutionService
  -> EF Core / SQLite
```

This is currently a single-project modular monolith. It is not yet a full Clean Architecture implementation, but it provides a practical foundation for learning backend architecture, background processing, reliability, persistence, and testing.

## Current Project Structure

```text
src/Api/JobProcessing.Api
  Controllers/
  Contracts/
  Enums/
  Infrastructure/
  Migrations/
  Models/
  Services/
```

All application code currently lives in one project. The folders provide organisation, but they do not yet enforce architectural boundaries.

Future versions may split the system into separate projects such as:

```text
JobProcessing.Domain
JobProcessing.Application
JobProcessing.Infrastructure
JobProcessing.Api
JobProcessing.Worker
```

That split is not required yet. The current priority is to understand the responsibilities clearly before introducing more structure.

## Component Responsibilities

### API Layer

Location:

```text
Controllers/
Contracts/
```

Responsibilities:

- Accept HTTP requests
- Create jobs
- Return job status
- Retry failed jobs
- Return basic metrics
- Convert persisted entities into API response DTOs

Current endpoints:

- `POST /jobs`
- `GET /jobs/{id}`
- `POST /jobs/{id}/retry`
- `GET /metrics`
- `GET /health`

The API currently depends directly on `AppDbContext`. This is acceptable for the current learning stage, but it means HTTP concerns and persistence concerns are still coupled.

Future improvement:

```text
Controller
  -> Application service
  -> Persistence abstraction or DbContext
```

### Background Worker

Location:

```text
Services/JobWorker.cs
```

Responsibilities:

- Run continuously using `BackgroundService`
- Create a scoped service provider for each polling cycle
- Query stuck `Processing` jobs
- Recover or fail stale jobs
- Query the next available `Pending` job
- Mark a job as `Processing`
- Save the claim attempt
- Handle optimistic concurrency conflicts
- Call `JobExecutionService`
- Persist the final job state
- Wait for the next polling interval

The worker currently contains several responsibilities in one class. This keeps the early implementation easy to follow, but it also makes the class harder to unit test.

Future improvement candidates:

- `JobClaimService`
- `JobRecoveryService`
- `JobProcessor`
- `RetryPolicy`
- `IClock` for testable time behavior

### Job Execution

Location:

```text
Services/JobExecutionService.cs
```

Responsibilities:

- Simulate job processing time
- Simulate success or failure
- Apply success behavior
- Apply failure behavior
- Increment retry count
- Set retry cooldown
- Mark jobs as permanently failed after max retry count
- Emit structured job result logs

This service currently combines execution simulation, retry decisions, status mutation, logging, randomness, and time handling.

That is acceptable for the current stage, but it is also one of the best places to improve testability later.

### Persistence Layer

Location:

```text
Infrastructure/
Migrations/
```

Responsibilities:

- Configure EF Core through `AppDbContext`
- Persist `JobEntity`
- Store job status
- Store retry count
- Store processing timestamps
- Store completion timestamps
- Store failure information
- Support migrations
- Experiment with optimistic concurrency using `RowVersion`

The current database provider is SQLite.

### Domain-Like Rules

Location:

```text
Enums/JobStatus.cs
Services/JobStateMachine.cs
```

Responsibilities:

- Define possible job statuses
- Define allowed status transitions

Current job statuses:

- `Pending`
- `Processing`
- `Success`
- `Failed`

The state machine currently defines valid transitions, but not every status change in the codebase is forced through it yet. This is an important future architecture improvement.

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

### Pending

The job has been created and is waiting for the worker.

### Processing

The worker has claimed the job and is executing it.

### Success

The job completed successfully.

### Failed

The job failed permanently after reaching the maximum retry count, or after recovery determined that it should no longer be retried.

## Processing Flow

```text
Client
  -> POST /jobs
  -> API creates JobEntity
  -> Status = Pending
  -> Save to database

Worker polling loop
  -> Recover stale Processing jobs
  -> Find oldest eligible Pending job
  -> Mark as Processing
  -> Save changes
  -> Execute job
  -> Mark as Success, Pending, or Failed
  -> Save changes
```

The worker uses polling rather than a message broker. This keeps the system simple while still allowing the project to explore background processing, retries, persistence, and concurrency.

## Retry and Recovery

The system supports simple retry behavior:

- Failed execution increments `RetryCount`
- If `RetryCount` is below the configured maximum, the job returns to `Pending`
- `NextRetryAtUtc` controls retry cooldown
- If the maximum retry count is reached, the job becomes `Failed`

The system also supports stuck job recovery:

- Jobs left in `Processing` beyond a configured timeout are considered stale
- Stale jobs are either returned to `Pending` or marked as `Failed`
- This protects the system from jobs being stuck forever after a worker crash or interrupted execution

## Concurrency

The system includes an optimistic concurrency concept using `RowVersion`.

The worker attempts to save a job after marking it as `Processing`. If another worker has already claimed the same job, EF Core can raise a concurrency exception and the worker skips that job.

This is an early concurrency model. It is useful for learning, but future work may include:

- Atomic claim query
- Better provider-specific concurrency handling
- Multiple workers
- Distributed locking if truly needed

## Observability

The system currently includes:

- Structured logging
- `JobResult` logs for execution outcomes
- `/health` endpoint
- `/metrics` endpoint with basic job counts

Future observability improvements may include:

- Correlation ID
- Request logging middleware
- OpenTelemetry
- Prometheus
- Grafana
- Distributed tracing

## Current Limitations

The current architecture intentionally keeps some trade-offs visible:

- API controllers access `AppDbContext` directly
- API and worker run in the same project and process
- `JobWorker` contains multiple responsibilities
- Retry and execution behavior are mixed in `JobExecutionService`
- Time and randomness are not abstracted, which makes unit testing harder
- State transitions are not consistently enforced through `JobStateMachine`
- Older in-memory prototype code still exists and should be cleaned up
- There are no automated tests yet

These limitations are not failures. They are useful learning points and provide a clear path for future refactoring.

## Architecture Direction

The next architecture improvements should be driven by real learning value and testability, not by adding patterns for their own sake.

Direction:

1. Add unit tests for state transition and DTO mapping rules.
2. Clean up legacy in-memory prototype code if it is no longer needed.
3. Extract retry and execution decisions from `JobExecutionService`.
4. Extract worker sub-responsibilities from `JobWorker`.
5. Introduce an application layer once controller and worker use cases become clearer.
6. Split projects only when the boundaries are understood well enough to justify the extra structure.

The goal is to grow toward cleaner architecture gradually while keeping the system understandable.
