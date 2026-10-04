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
