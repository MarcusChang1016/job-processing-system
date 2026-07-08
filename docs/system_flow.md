# Job Processing System - System Flow

This document describes how a job moves through the system from creation to completion.

## High-Level Flow

```text
Client
  -> POST /jobs
  -> API creates a `Pending` job
  -> EF Core saves the job to SQLite

Background worker
  -> calls `JobRecoveryService` to recover stale `Processing` jobs
  -> polls for eligible `Pending` jobs
  -> marks one job as `Processing`
  -> calls `JobExecutionService`
  -> `JobExecutionService` simulates execution
  -> `JobExecutionResultHandler` applies success or failure state changes
  -> `JobRetryPolicy` decides retry behaviour for failed attempts
  -> EF Core saves the final state
```

## 1. Job Creation

The client sends:

```text
POST /jobs
```

The API creates a new job with:

- `Id`
- `Status = Pending`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `RetryCount = 0`

The job is saved to SQLite through EF Core.

At this point, the job has not been processed yet. It is only waiting for the background worker.

## 2. Worker Polling

The background worker runs continuously using `BackgroundService`.

On each polling cycle, the worker:

1. Creates a scoped service provider.
2. Resolves `AppDbContext`.
3. Resolves `JobExecutionService`.
4. Calls `JobRecoveryService` to recover stuck `Processing` jobs.
5. Looks for the oldest eligible `Pending` job.

A pending job is eligible when:

- `Status = Pending`
- `RetryCount` is below the configured maximum
- `NextRetryAtUtc` is either empty or already reached

## 3. Stuck Job Recovery

When a stuck job is found, `JobRecoveryService` treats it as a failed attempt and delegates retry behaviour to `JobRetryPolicy`.

A job is considered stuck when:

- `Status = Processing`
- `ProcessingStartedAtUtc` has a value
- `ProcessingStartedAtUtc` is older than the configured stuck job timeout

When a stuck job is found, the worker:

- increments `RetryCount`
- updates `UpdatedAtUtc`
- clears `ProcessingStartedAtUtc`
- stores a recovery error message
- applies retry cooldown through `NextRetryAtUtc` when retry is allowed

Then `JobRetryPolicy` decides:

```text
RetryCount < MaxRetryCount
  -> Status = Pending
  -> NextRetryAtUtc = now + RetryCooldownSeconds

RetryCount >= MaxRetryCount
  -> Status = Failed
  -> NextRetryAtUtc = null
```

This prevents jobs from staying in `Processing` forever after a crash, cancellation, or interrupted execution.

## 4. Job Claiming

After recovery, the worker looks for the oldest eligible pending job.

When it finds one, it attempts to claim it by setting:

- `Status = Processing`
- `ProcessingStartedAtUtc = now`
- `UpdatedAtUtc = now`

The worker then saves the change to the database.

If another worker has already claimed the same job, EF Core may raise a concurrency exception. In that case, the worker skips the job and waits for the next polling cycle.

This is an early optimistic concurrency model. It is useful for learning, but future versions may improve this with an atomic claim operation.

## 5. Job Execution

After a job is claimed, the worker calls `JobExecutionService`.

The current implementation simulates work by:

- waiting for a configured delay
- randomly succeeding or failing

`JobExecutionService` orchestrates the execution attempt. It uses `TimeProvider` for execution timestamps and delegates state changes to `JobExecutionResultHandler`.

This keeps the project focused on the job processing lifecycle before introducing real job handlers.

## 6. Successful Execution

When execution succeeds, `JobExecutionResultHandler` applies the success state:

```text
Status = Success
CompletedAtUtc = now
UpdatedAtUtc = now
LastErrorMessage = null
NextRetryAtUtc = null
ProcessingStartedAtUtc = null
```

The result is also written through structured logging.

## 7. Failed Execution and Retry

When execution fails, `JobExecutionResultHandler` delegates failure handling to `JobRetryPolicy`.

```text
RetryCount += 1
UpdatedAtUtc = now
LastErrorMessage = error message
ProcessingStartedAtUtc = null
```

Then retry rules are applied.

If the job still has retries remaining:

```text
Status = Pending
NextRetryAtUtc = now + RetryCooldownSeconds
```

The job will not be picked up again until the cooldown has passed.

If the job has reached the maximum retry count:

```text
Status = Failed
NextRetryAtUtc = null
```

At that point, automatic retry stops.

## 8. Manual Retry

The client can retry a permanently failed job by sending:

```text
POST /jobs/{id}/retry
```

The API only allows this when the current status is `Failed`.

When retry is allowed, the job is reset:

```text
Status = Pending
RetryCount = 0
UpdatedAtUtc = now
NextRetryAtUtc = null
```

The worker can then pick it up again in a future polling cycle.

## 9. Reading Job State

The client can query a job by sending:

```text
GET /jobs/{id}
```

The API reads the job from the database and maps it to `JobResponse`.

The response includes:

- job id
- status
- created timestamp
- updated timestamp
- retry count
- completed timestamp
- failure reason when applicable

## 10. Metrics Flow

The client can query basic system metrics:

```text
GET /metrics
```

The API counts jobs by status:

- pending jobs
- processing jobs
- failed jobs
- successful jobs

This is a simple observability endpoint. Future versions may replace or extend this with Prometheus and OpenTelemetry.

## State Transition Summary

```text
Pending
  -> Processing

Processing
  -> Success
  -> Pending
  -> Failed

Failed
  -> Pending

Success
  -> terminal state
```

## Important Design Notes

- The system currently uses polling rather than a message queue.
- The API and worker currently run in the same process.
- Job state is persisted in SQLite.
- Retry behaviour is time-based through `NextRetryAtUtc`.
- Stuck job recovery protects against jobs remaining in `Processing` forever.
- The current execution logic is simulated and intentionally simple.
- `JobExecutionService` uses `TimeProvider` for execution timestamps.
- Success and failure reactions are handled by `JobExecutionResultHandler`.
- Retry decisions are handled by `JobRetryPolicy`.
- State transitions exist in `JobStateMachine`, but not every mutation is enforced through it yet.
