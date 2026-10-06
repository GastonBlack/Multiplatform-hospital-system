# ADR-0010 — Reschedule the Existing Appointment

## Status

Accepted for Version 1. The detailed concurrency protocol still requires implementation design and testing.

## Context

Rescheduling changes an appointment's time. It must not lose the original reservation when the new interval is unavailable.

The current scope does not require replacement appointment records or a separate rescheduling-history table.

## Decision

Update the existing Appointment inside one transaction rather than cancelling it and creating another row.

- Preserve Id, PatientId, DoctorId, MedicalSpecialtyId, CreatedAt, and Scheduled status.
- Change StartTime and EndTime to the validated replacement interval and update UpdatedAt.
- Allow only future Scheduled appointments to be rescheduled by the patient or an authorized receptionist.
- Validate all booking rules, including patient eligibility, specialty assignment, doctor availability, and the ADR-0009 time policy.
- Exclude the appointment being rescheduled from its own conflict check. Other Scheduled appointments continue to block conflicting intervals.
- Commit only when the replacement interval is valid and persisted successfully. Any failure must roll back and leave the original row unchanged.

The old interval is released and the new interval reserved as one committed change. There is no intermediate committed cancellation or replacement appointment.

This workflow changes time only. Changing the patient, doctor, or specialty is outside this rescheduling operation.

Coordinate concurrent changes to the same appointment, including another rescheduling, cancellation, or completion. The implementation must validate current state within its concurrency protocol rather than writing over stale data. Its locking or concurrency-check mechanism remains to be selected.

Version 1 retains the current interval and UpdatedAt, not a full history of previous intervals. Appointment history still means past appointments; it does not imply a rescheduling audit trail.

## Alternatives Considered

- Cancel and create in separate transactions: can lose the original reservation if replacement creation fails.
- Cancel and create atomically: can preserve atomicity, but introduces a new identifier and unnecessary record/link management for a time-only change.
- A separate rescheduling-history table: preserves every previous interval but adds data and behavior not required for Version 1.

## Consequences

- Links and references retain the same Appointment.Id after rescheduling.
- Existing database conflict protection also applies to interval updates.
- API conflict handling must preserve the original appointment on any failed transaction.
- Previous time values are not retained as an audit history.
- Integration tests must verify successful updates, unchanged original data after failure, preserved identity and associations, and concurrent changes to the same appointment.

## Revisit When

A rescheduling audit trail or a replacement-appointment workflow becomes a real requirement.

## References

- [Domain Model](../DomainModel.md)
- [Scheduling Concurrency](0005-scheduling-concurrency.md)
- [Appointment Time Policy](0009-appointment-time-policy.md)
- [ERD](../ERD.md)
