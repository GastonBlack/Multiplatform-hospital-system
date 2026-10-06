# ADR-0003 — Shared PostgreSQL Database and EF Core Context

## Status

PostgreSQL as the authoritative database is accepted. A shared HospitalDbContext is the proposed initial persistence arrangement.

## Context

Registration creates both an account and a profile. Verification can update patient audit data and account status. Scheduling validates information owned by several modules.

These operations need atomic persistence within the initial monolith.

## Decision

Use one PostgreSQL database as the authority for persistent account and scheduling data.

Propose one scoped HospitalDbContext shared by participating services, with EF Core configurations and migrations under Infrastructure/Persistence.

The service coordinating a write workflow owns its transaction and commit. Supporting module operations participate in that workflow rather than independently committing partial changes.

Services may use EF Core for their own module's data. Cross-module contracts must not expose DbContext, DbSet, IQueryable, or mutable tracked entities.

## Alternatives Considered

- Separate databases per module: clearer storage isolation, but complicates atomic operations across accounts and scheduling.
- Separate contexts sharing one database: stronger mapping separation, but requires additional coordination for transactions spanning contexts.
- A generic repository and unit-of-work layer: adds abstractions without a demonstrated need in the current design.

## Consequences

- A shared database supports relational constraints and transactions across the initial workflows.
- A shared context simplifies participation in one transaction, but also allows accidental access to another module's data.
- Services must be used sequentially within a workflow; the shared context is not intended for concurrent operations.
- Shared persistence increases coupling if a module is later extracted.
- A transaction alone does not solve every concurrency race. Constraints and a coordinated locking or isolation strategy remain necessary.

## Revisit When

The shared context makes ownership difficult to maintain or a module gains an independent deployment and persistence requirement.

## References

- [Architecture — Persistence and Transactions](../Architecture.md#7-persistence-and-transactions)
- [ERD](../ERD.md)
- [EF Core DbContext lifetime and threading](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues)
