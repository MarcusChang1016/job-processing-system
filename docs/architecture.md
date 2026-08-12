# Job Processing System - Architecture

## Overview

The current system is an ASP.NET Core application that hosts both a Web API and a background worker in the same process.

The API is responsible for accepting client requests and exposing job state. Job creation and reads currently use `AppDbContext` directly, while manual retry is delegated to `ManualJobRetryService`. The worker orchestrates polling, recovery, claiming, and execution by delegating recovery to `JobRecoveryService`, claiming to `JobClaimService`, processing to `JobProcessor`, execution to `JobExecutionService`, and failed-attempt decisions to `JobRetryPolicy`.

The current architecture is intentionally simple:

```text
Client
  -> ASP.NET Core API
       -> Create/Get endpoints -> AppDbContext
       -> Retry endpoint -> ManualJobRetryService -> AppDbContext

BackgroundService worker
  -> JobRecoveryService
  -> JobClaimService
  -> JobProcessor
  -> JobExecutionService
  -> JobExecutionResultHandler
  -> JobRetryPolicy

AppDbContext -> EF Core / SQLite
```

This is currently a single-project modular monolith. It is not yet a full Clean Architecture implementation, but it provides a practical foundation for learning backend architecture, background processing, reliability, persistence, and testing.

## Current Project Structure

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

All application code currently lives in one project. The folders express boundaries inside the modular monolith, but they do not yet enforce architectural boundaries at the project/assembly level.

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
Api/Controllers/
Api/Contracts/
```

Responsibilities:

- Accept HTTP requests
- Create jobs
- Return job status
- Map manual retry outcomes to HTTP responses
- Return basic metrics
- Convert persisted entities into API response DTOs

Current endpoints:

- `POST /jobs`
- `GET /jobs/{id}`
- `POST /jobs/{id}/retry`
- `GET /metrics`
- `GET /health`

The create and get endpoints currently depend directly on `AppDbContext`. The manual retry endpoint delegates its use-case rules and persistence to `ManualJobRetryService`, while the controller remains responsible for mapping outcomes to HTTP status codes, `ProblemDetails`, and response DTOs.

Future improvement:

```text
Controller
  -> Application service
  -> DbContext
```

### Manual Job Retry

Location:

```text
Application/Jobs/ManualJobRetryService.cs
Application/Jobs/ManualJobRetryResult.cs
Application/Jobs/ManualJobRetryOutcome.cs
Application/Jobs/JobDetails.cs
```

Responsibilities:

- Find the requested job
- Allow manual retry only when the job is `Failed`
- Return the job to `Pending`
- Reset the automatic retry count and clear previous execution state
- Use `TimeProvider` when updating `UpdatedAtUtc`
- Persist the state change
- Return HTTP-independent `Succeeded`, `NotFound`, or `InvalidState` outcomes
- Return successful job data as application-level `JobDetails` rather than exposing `JobEntity`

`ManualJobRetryService` keeps the manual retry use case out of `JobsController`. The controller maps its result to `200 OK`, `404 Not Found`, or `400 Bad Request` and owns the API-specific `ProblemDetails` response.

Manual retry is distinct from automatic retry. `JobRetryPolicy` decides what happens after a failed execution attempt, while `ManualJobRetryService` handles an explicit client request to restart a job that has already reached `Failed`.

On success, the service maps the persisted `JobEntity` to an immutable `JobDetails` read model. The controller then maps `JobDetails` to `JobResponse`, keeping EF Core persistence models out of the manual retry application contract.

### Background Worker

Location:

```text
Worker/JobWorker.cs
```

Responsibilities:

- Run continuously using `BackgroundService`
- Create a scoped service provider for each polling cycle
- Call `JobRecoveryService` to recover stale `Processing` jobs
- Call `JobClaimService` to claim the next eligible `Pending` job
- Call `JobProcessor` to process a claimed job
- Wait for the next polling interval

The worker is now mostly an orchestrator that coordinates scoped services and scheduling.

Future improvement candidates:

- Improve claim concurrency with an atomic claim operation
- Extract real job handlers when execution is no longer simulated

### Job Recovery

Location:

```text
Worker/JobRecoveryService.cs
```

Responsibilities:

- Find stale `Processing` jobs
- Treat recovered stale jobs as failed attempts
- Delegate retry/failure state changes to `JobRetryPolicy`
- Persist recovered job state
- Return the number of recovered jobs

`JobRecoveryService` keeps recovery logic out of `JobWorker`. This makes the worker thinner and allows recovery behaviour to be tested without testing the full background loop.

### Job Claiming

Location:

```text
Worker/JobClaimService.cs
```

Responsibilities:

- Find the oldest eligible `Pending` job
- Respect retry cooldown through `NextRetryAtUtc`
- Ignore jobs that reached the maximum retry count
- Mark the claimed job as `Processing`
- Set `ProcessingStartedAtUtc` and `UpdatedAtUtc`
- Persist the claim attempt
- Handle optimistic concurrency conflicts by returning no claimed job

`JobClaimService` keeps job selection and claim persistence out of `JobWorker`. This makes claim behaviour easier to test without testing the full background loop.

### Job Processing

Location:

```text
Worker/JobProcessor.cs
```

Responsibilities:

- Accept a claimed `Processing` job
- Skip jobs that are not in `Processing`
- Call `JobExecutionService` to execute the job
- Persist execution result changes

`JobProcessor` keeps execution persistence out of `JobWorker` while allowing `JobExecutionService` to focus on execution orchestration and result handling.

### Job Execution

Location:

```text
Worker/JobExecutionService.cs
```

Responsibilities:

- Orchestrate one job execution attempt
- Simulate job processing time
- Simulate success or failure
- Delegate success and failure reactions to `JobExecutionResultHandler`
- Emit structured job result logs

`JobExecutionService` no longer owns retry decisions or direct success/failure state mutation. It coordinates the execution flow and delegates result handling to `JobExecutionResultHandler`.

It still owns simulated randomness, delay, logging, and `TimeProvider` usage. These are future testability improvement points.

### Job Execution Result Handler

Location:

```text
Worker/JobExecutionResultHandler.cs
```

Responsibilities:

- Apply success state changes to a job
- Mark successful jobs as `Success`
- Set `UpdatedAtUtc` and `CompletedAtUtc`
- Clear previous failure messages after success
- Delegate failure handling to `JobRetryPolicy`

This handler exists so `JobExecutionService` does not need to know the details of how job state changes after success or failure.

### Job Retry Policy

Location:

```text
Worker/JobRetryPolicy.cs
```

Responsibilities:

- Increment retry count after a failed attempt
- Record the failure or recovery message
- Clear `ProcessingStartedAtUtc`
- Update the job timestamp
- Decide whether the job should return to `Pending`
- Set `NextRetryAtUtc` when retry is allowed
- Mark the job as `Failed` when the maximum retry count is reached

This policy is unit tested because retry behaviour is a core business rule.
`JobRetryPolicy` is used by `JobExecutionResultHandler` when execution fails and by `JobRecoveryService` when a stale processing job is recovered.

### Persistence Layer

Location:

```text
Infrastructure/
Infrastructure/Entities/
Infrastructure/Migrations/
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
Domain/Jobs/JobStatus.cs
Domain/Jobs/JobStateMachine.cs
Domain/Jobs/JobResult.cs
```

Responsibilities:

- Define possible job statuses
- Define allowed status transitions
- Define job execution result log shape

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
  -> `JobRecoveryService` recovers stale Processing jobs through `JobRetryPolicy`
  -> `JobClaimService` claims the oldest eligible Pending job
  -> `JobProcessor` processes the claimed job
  -> `JobExecutionService` executes the job
  -> `JobExecutionResultHandler` and `JobRetryPolicy` apply result state changes
  -> `JobProcessor` saves execution result changes
```

The worker uses polling rather than a message broker. This keeps the system simple while still allowing the project to explore background processing, retries, persistence, and concurrency.

## Retry and Recovery

The system supports simple retry behaviour through `JobRetryPolicy`:

- A failed attempt increments `RetryCount`
- If `RetryCount` is below the configured maximum, the job returns to `Pending`
- `NextRetryAtUtc` controls retry cooldown
- If the maximum retry count is reached, the job becomes `Failed`

The system also supports stuck job recovery:

- Jobs left in `Processing` beyond a configured timeout are considered stale
- Stale jobs are either returned to `Pending` or marked as `Failed`
- This protects the system from jobs being stuck forever after a worker crash or interrupted execution

Stuck job recovery now uses the same retry policy as execution failure.

Manual retry is handled separately by `ManualJobRetryService`:

- Only jobs already in `Failed` can be manually retried
- A manual retry returns the job to `Pending`
- The automatic retry count is reset so the new processing cycle has a fresh retry budget
- Previous retry scheduling, completion, processing, and error state is cleared

## Concurrency

The system includes an optimistic concurrency concept using `RowVersion`.

`JobClaimService` attempts to save a job after marking it as `Processing`. If another worker has already claimed the same job, EF Core can raise a concurrency exception and the worker skips that job.

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

- The create and get job endpoints still access `AppDbContext` directly; manual retry now uses a dedicated application-style service.
- API and worker run in the same project and process.
- `JobWorker` still orchestrates the polling loop and scoped worker services, but worker code is now grouped under `Worker/`.
- `JobExecutionService` uses `TimeProvider` for execution timestamps, but randomness and delay are still not abstracted.
- State transitions are not consistently enforced through `JobStateMachine`.
- Test coverage includes state transitions, DTO mapping, retry policy behaviour, recovery, claiming, execution result handling, database-backed manual retry service tests, and Jobs API integration tests.
- `JobProcessor` is intentionally thin and currently has limited direct test coverage because `JobExecutionService` is still concrete and simulation-heavy.

These limitations are not failures. They are useful learning points and provide a clear path for future refactoring.

## Architecture Direction

The next architecture improvements should be driven by real learning value and testability, not by adding patterns for their own sake.

Direction:

1. Continue expanding unit tests around job execution orchestration and worker behaviour.
2. Improve testability around time, randomness, and execution simulation.
3. Improve execution testability before extracting more worker responsibilities.
4. Continue extracting focused application use cases when controller or worker responsibilities justify it.
5. Split projects only when the boundaries are understood well enough to justify the extra structure.

The goal is to grow toward cleaner architecture gradually while keeping the system understandable.
