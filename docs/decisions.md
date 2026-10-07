# Engineering Decisions

## Use PostgreSQL row locking for atomic job claiming

- Date: 2026-09-29
- Status: Accepted

### Context

Optimistic concurrency through PostgreSQL `xmin` prevented two workers from successfully updating the same job, but both workers could still select the same pending row. The losing worker used a concurrency exception as normal control flow and waited until the next polling cycle even when another job was available.

### Decision

`JobClaimService` claims work inside an explicit transaction. It selects the oldest eligible job with `FOR UPDATE SKIP LOCKED`, updates the job to `Processing`, and commits the transaction. The existing `xmin` token remains as defence against stale tracked entities.

The PostgreSQL-specific SQL stays inside `JobClaimService`; its public interface remains `ClaimNextJobAsync(CancellationToken)`.

### Alternatives considered

- Keep optimistic claiming only: correct but causes avoidable contention and exception-driven control flow.
- Use a single CTE with `UPDATE ... RETURNING`: fewer round trips but more complex EF Core mapping for the current stage.
- Add a Redis distributed lock: introduces another system and cross-store consistency concerns for a problem PostgreSQL already solves.

### Consequences

- Multiple workers can skip locked jobs and claim other available work.
- Selection and state transition are protected by one transaction.
- Claiming is intentionally coupled to PostgreSQL.
- The query should be measured under realistic load before adding an index or moving to a single-statement claim.

## Use one fixed BOM weather job as the first real workload

- Date: 2026-10-08
- Status: Accepted

### Context

Random success and failure exercised the job state machine but could not show how
a real external dependency produces a result or a retryable failure. The project
is primarily for learning the processing system, not building a weather platform.

### Decision

Every submitted job fetches the latest Brisbane observation from the public BOM
API. It takes no input. The client checks HTTP status and maps the observation's
UTC timestamp and air temperature; success stores those values in nullable columns
on `JobEntity` and exposes them through the job read response. Fetch or parsing
errors use the existing retry policy. A request cancellation while the worker is
still running counts as a failed attempt; worker shutdown cancellation propagates.

### Alternatives considered

- Keep random simulation: smaller, but no real HTTP, parsing, or result-persistence
  behaviour to demonstrate.
- Build a scheduled API/database-to-S3 pipeline: realistic, but adds scheduling,
  storage, credentials, and data-transformation scope unrelated to this milestone.
- Add generic job types and separate result storage now: more extensible, but adds
  abstractions before a second job type or a larger result creates that need.

### Consequences

- The smallest end-to-end slice now covers an external HTTP call, validation,
  persistence, API readback, retry, timeout, and cancellation tests.
- Public BOM availability and data shape affect job success; the client has a
  ten-second timeout and failures may exhaust the retry budget.
- The result columns and worker are deliberately specific to this one job. Revisit
  their shape if another job type creates real pressure; do not generalise early.
- Repeating a fetch does not create an external write, though the latest BOM value
  may change between attempts. This is not a general idempotency solution.
