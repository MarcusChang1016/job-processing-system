# Job Processing System - Architecture

## Overview

This system is an ASP.NET Core application that runs a Web API and a `BackgroundService` worker in the same process. The API is responsible for handling job lifecycle operations, while the worker processes jobs asynchronously in the background. Both components share a singleton `JobStore`, which acts as the in-memory state layer.

---

## Components

### JobsController

Handles three endpoints under the `/jobs` route prefix.

- `POST /jobs`  
  Creates a new job with status `Pending`.

- `GET /jobs/{id}`  
  Returns the current state of a job (`id`, `status`, `createdAt`, `updatedAt`, `retryCount`).  
  Returns `404` if the job does not exist.

- `POST /jobs/{id}/retry`  
  Resets a `Failed` job back to `Pending` and increments `retryCount`.  
  Returns `400` if the job is not in the `Failed` state.

---

### JobStore

A singleton in-memory store backed by a `List<Job>`.

Responsibilities:

- Store job data
- Provide access to jobs
- Update job state

It exposes:

- `AddJob`
- `GetJob`
- `GetPendingJobs`
- `UpdateJob`

Updates mutate the existing job instance in place. The store is intentionally simple and not thread-safe, as concurrency is not part of the MVP scope. There is no persistence — all data is lost on application restart.

---

### Job

Represents a unit of work.

Fields:

- `Id` (Guid)
- `Status` (`Pending`, `Processing`, `Success`, `Failed`)
- `CreatedAt` (UTC)
- `UpdatedAt` (UTC)
- `RetryCount`

After creation, only state transitions and retry count are mutated.

---

### JobResult

Represents the outcome of a single job execution.

Fields:

- `JobId`
- `Success`
- `RetryCount`
- `StartedAtUtc`
- `FinishedAtUtc`
- `ErrorMessage` (optional)

`JobResult` is not persisted.  
It is emitted via structured logging and used for observability.

---

### JobWorker

A `BackgroundService` that continuously polls for pending jobs.

Behavior:

- Runs every 3 seconds
- Picks the first `Pending` job
- Transitions it to `Processing`
- Simulates execution (2 seconds delay)
- Produces either:
  - `Success`
  - `Failed`

Failure is simulated for demonstration purposes.

---

### Error Handling Strategy

The worker follows a **fail-fast approach**:

- Any exception during execution immediately stops the job
- Errors are caught at the job boundary (not per operation)
- Job status is updated to `Failed`
- A structured `JobResult` is logged

This ensures:

- No partial execution
- Consistent state transitions
- Centralized error handling

---

## Job Lifecycle

```
Pending → Processing → Succeeded
                    ↘ Failed → (retry) → Pending
```

- `Pending` → job created, waiting to be processed
- `Processing` → worker is executing the job
- `Succeeded` → job completed successfully
- `Failed` → execution failed

Failed jobs can be retried via API, which resets them to `Pending` and increments `RetryCount`.

---

## Execution Flow

```
Client          POST /jobs
                  └─► JobStore.AddJob()  →  Status: Pending

JobWorker       polls every 3s
                  └─► Status: Processing
                  └─► Task.Delay(2s)
                  └─► 50% Success  →  Status: Succeeded  →  log JobResult
                      50% Failure  →  Status: Failed     →  log JobResult + ErrorMessage

Client          GET  /jobs/{id}           query current status
                POST /jobs/{id}/retry     re-enqueue a failed job

                Failures are handled using a fail-fast approach, where exceptions immediately terminate execution and are handled centrally.

```

## Scope and Limitations

This system intentionally excludes:

- Persistent storage (in-memory only)
- Distributed processing or multiple workers
- Message queues or external infrastructure
- Parallel job execution

The goal is to demonstrate:

- Job lifecycle modeling
- Error handling strategy
- Clear architectural boundaries

rather than infrastructure complexity.
