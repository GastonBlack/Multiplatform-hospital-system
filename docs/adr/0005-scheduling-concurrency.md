# ADR-0005 — Database Scheduling Protection and Atomic Rescheduling

## Status

Database-enforced conflict protection and atomic rescheduling are accepted requirements. The exclusion-constraint mechanism is proposed; the complete transaction strategy remains pending.

## Context

Multiple users may attempt conflicting bookings through different API instances. A check followed by an insert can race with another request.

Rescheduling must not cause the patient to lose the original appointment if the replacement interval is unavailable.

## Decision

Enforce scheduling conflicts against PostgreSQL rather than process-local memory or Redis.

Propose GiST exclusion constraints combining DoctorId equality with overlapping tstzrange intervals. Use the btree_gist extension for UUID equality.

Apply appointment exclusion only to Scheduled rows and availability exclusion to all DoctorAvailability periods. Use half-open intervals [StartTime, EndTime) so adjacent appointments can share a boundary without overlapping.

Execute rescheduling as one transaction. If the replacement cannot be secured, preserve the original appointment unchanged. Updating the existing Appointment row is a candidate, not yet an accepted history policy.

## Alternatives Considered

- Application check followed by insertion: insufficient against simultaneous writes.
- An in-process lock: does not coordinate different API instances.
- Redis locks as the booking authority: introduce another coordination mechanism while PostgreSQL still stores the authoritative result.
- Database locking or serializable transactions without exclusion constraints: possible alternatives, but require a carefully designed protocol and conflict handling.

## Consequences

- PostgreSQL can reject overlapping writes regardless of the API instance processing them.
- Constraints must be accompanied by API conflict handling and PostgreSQL integration tests.
- Booking versus availability edits, account-status changes, or specialty-assignment changes still needs coordinated validation and transaction design.
- Exclusion constraints do not enforce exactly-one-profile or global EmployeeNumber uniqueness. Number ownership is handled separately by [ADR-0007](0007-employee-number-registry.md); profile exclusivity remains pending.
- Migration support, extension availability, rollback behavior, and concurrent operations must be validated before this proposal is finalized.

## Revisit When

Persistence testing demonstrates that the proposed mechanism cannot satisfy the required scheduling or deployment behavior.

## References

- [ERD — Scheduling Concurrency](../ERD.md#6-scheduling-concurrency)
- [Business Rules](../BusinessRules.md)
- [PostgreSQL range constraints](https://www.postgresql.org/docs/current/rangetypes.html#RANGETYPES-CONSTRAINT)
- [PostgreSQL btree_gist](https://www.postgresql.org/docs/current/btree-gist.html)
