# ADR-0005 — Database Scheduling Protection and Atomic Rescheduling

## Status

Accepted for Version 1. Row-lock coordination, exclusion constraints, and atomic rescheduling are the selected design; migrations and PostgreSQL integration tests remain implementation work.

## Context

Multiple users may attempt conflicting bookings through different API instances. A check followed by an insert can race with another request.

Rescheduling must not cause the patient to lose the original appointment if the replacement interval is unavailable.

## Decision

Enforce scheduling conflicts against PostgreSQL rather than process-local memory or Redis.

Use GiST exclusion constraints combining DoctorId equality with overlapping tstzrange intervals. Use the btree_gist extension for UUID equality.

Apply appointment exclusion only to Scheduled rows and availability exclusion to all DoctorAvailability periods. Use half-open intervals [StartTime, EndTime) so adjacent appointments can share a boundary without overlapping.

Execute rescheduling as one transaction updating the existing Appointment's time, as accepted in [ADR-0010](0010-rescheduled-existing-appointment.md). Preserve its Id and associations; if the replacement interval cannot be secured, roll back and preserve the original appointment unchanged. No replacement row or separate rescheduling-history table is introduced in Version 1.

### Transaction and Lock Order

Use explicit, short PostgreSQL transactions at READ COMMITTED isolation. Acquire the required row locks before validating or writing, and hold them until commit or rollback. Slot-list reads remain advisory and do not reserve anything.

Acquire locks in this order, sorting multiple identifiers within each table by Id:

1. User rows: the acting account and affected patient/doctor accounts. Use FOR SHARE for eligibility reads and FOR UPDATE for account-status, profile-permission, or patient-verification changes.
2. MedicalSpecialty rows: FOR SHARE when validating IsActive or changing doctor assignments; FOR UPDATE when changing specialty state.
3. Doctor rows: FOR UPDATE for every appointment mutation, availability mutation, or doctor-specialty assignment mutation affecting that doctor.
4. Existing Appointment rows: FOR UPDATE before rescheduling, cancelling, or completing them.

Choose the strongest required mode initially; do not upgrade a shared lock later. Acquire only rows required by the use case. An account-status or specialty-state change need not lock every affected doctor: its exclusive parent-row lock coordinates with scheduling's shared eligibility lock. Mutations of patient verification or StaffRole must first lock the owning User exclusively even though the changed field is in a profile table.

Preliminary reads may discover identifiers, but are not eligibility decisions. After obtaining the locks, reload current data, including previously tracked EF Core entities, and perform fresh queries for assignments, availability, and Scheduled conflicts. Do not reuse a pre-lock snapshot. Missing rows or changed associations invalidate the operation.

The coordinating service owns the transaction. Supporting module services use that same context/transaction and lock order, without independent commits or service-call cycles. Authentication session operations invoked by an account change follow the User locks; refresh or logout must not hold a session lock and then request a conflicting User lock.

### Protected Workflows

- Booking and rescheduling validate current actor permissions, patient eligibility, doctor account state, specialty state/assignment, time policy, availability containment, and Scheduled conflicts after locking. Exclusion constraints remain the final overlap safeguard.
- Availability edits lock the doctor before checking Scheduled appointments. Reject an edit that leaves an existing Scheduled appointment outside valid availability; reception must cancel affected appointments first under ADR-0011. A competing booking either commits before the edit's fresh check or validates against the committed edit.
- Doctor-specialty assignment changes lock the specialty and doctor before mutating the association. After locking, reject removal if the DoctorId and MedicalSpecialtyId pair has future Scheduled appointments (StartTime greater than the current instant at validation). Reception must cancel affected appointments first with a patient-visible reason and the existing in-app guidance. Cancelled, Completed, and past appointments do not block removal and are preserved. Perform the fresh blocking-appointment query and association deletion in the same transaction. If booking commits first, removal sees the appointment and fails; if removal commits first, booking sees the missing assignment and fails.
- Cancellation, completion, and rescheduling lock the same doctor and appointment, reload current state, and validate the permitted transition. Scheduled remains the only state from which cancellation or completion can proceed.
- Account or specialty deactivation that commits first blocks subsequent booking/rescheduling validation. A booking that commits first remains an existing appointment for receptionist review under ADR-0011; deactivation does not retroactively cancel it.

Appointment mutation requests carry the expected UpdatedAt from the appointment read. Compare it with the locked row and reject stale requests without changing the appointment. Each successful mutation must persist a strictly newer UpdatedAt at PostgreSQL timestamp precision; DTO serialization must preserve that precision. This prevents a second request from silently overwriting an intervening time or status change without adding a history table or domain attribute.

### Failure Handling and Verification

Roll back the entire use case on failed validation, an exclusion violation, a stale appointment, or a database failure. Return a controlled conflict for an occupied interval or stale appointment so the client can refresh. Do not blindly retry a business conflict. Deadlock or lock-timeout handling must roll back; any bounded retry reruns the entire transaction, including reads and validation.

Keep transactions free of external calls or user interaction. Existing in-flight request semantics from ADR-0012 remain applicable; these locks do not promise to interrupt a request when logout occurs.

PostgreSQL integration tests must cover simultaneous same-slot bookings across connections, booking versus availability changes, verification/account/role changes, specialty deactivation and assignment changes, concurrent appointment mutations, stale UpdatedAt rejection, and complete rollback of failed rescheduling. Verify lock ordering alongside authentication revocation, timestamp precision, btree_gist availability, and migration rollback. Documentation selects the design; it does not establish that these tests have already passed.

## Alternatives Considered

- Application check followed by insertion: insufficient against simultaneous writes.
- An in-process lock: does not coordinate different API instances.
- Redis locks as the booking authority: introduce another coordination mechanism while PostgreSQL still stores the authoritative result.
- SERIALIZABLE transactions as the primary protocol: possible, but require whole-transaction serialization retries; explicit parent-row locks make the initial coordination scope easier to follow.
- Locks per date or slot: permit more parallel writes for one doctor, but introduce additional coordination keys for availability and assignment changes. Reconsider only if doctor-level contention is measured.

## Consequences

- PostgreSQL can reject overlapping writes regardless of the API instance processing them.
- Constraints must be accompanied by API conflict handling and PostgreSQL integration tests.
- Writes for one doctor wait for one another, including writes on different dates; shared patient or actor eligibility reads do not exclusively lock those accounts.
- All participating mutation paths must follow the protocol. Exclusion constraints alone do not enforce cross-table eligibility or availability containment.
- Exclusion constraints do not enforce exactly-one-profile or global EmployeeNumber uniqueness. Number ownership is handled separately by [ADR-0007](0007-employee-number-registry.md); profile exclusivity and existence use [ADR-0008](0008-single-user-profile.md).
- Migration support, extension availability, rollback behavior, timestamp precision, and concurrent operations must be validated during implementation.

## Revisit When

Persistence testing demonstrates that the mechanism cannot satisfy the required scheduling or deployment behavior, or measured doctor-level contention justifies finer locking.

## References

- [ERD — Scheduling Concurrency](../ERD.md#6-scheduling-concurrency)
- [Business Rules](../BusinessRules.md)
- [PostgreSQL range constraints](https://www.postgresql.org/docs/current/rangetypes.html#RANGETYPES-CONSTRAINT)
- [PostgreSQL btree_gist](https://www.postgresql.org/docs/current/btree-gist.html)
- [PostgreSQL explicit locking](https://www.postgresql.org/docs/current/explicit-locking.html)
- [PostgreSQL transaction isolation](https://www.postgresql.org/docs/current/transaction-iso.html)
