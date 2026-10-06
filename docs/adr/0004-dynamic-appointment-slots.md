# ADR-0004 — Calculate Appointment Slots Dynamically

## Status

Accepted for Version 1. The appointment-duration policy remains pending.

## Context

Doctors define availability periods. Patients need a list of bookable intervals within those periods, excluding occupied times.

The current domain does not require slot-specific state, temporary holds, or a separately managed inventory of slots.

## Decision

Persist DoctorAvailability periods and Appointment intervals. Calculate available slots dynamically from those periods, the applicable appointment duration, existing Scheduled appointments, and scheduling rules.

Do not introduce an AppointmentSlot table in Version 1.

Availability owns the periods. Appointments owns slot calculation because it combines those periods with appointment occupancy.

Booking and rescheduling must validate the selected interval again against authoritative data. A slot returned in a read response is not a reservation.

## Alternatives Considered

- Persist every slot: simplifies treating slots as inventory, but adds generation and synchronization work whenever availability or duration changes.
- Cache slots as booking authority: reduces some reads, but stale data cannot guarantee appointment consistency.

## Consequences

- Availability edits do not require regenerating a separate slot inventory.
- Slot reads must query relevant periods and appointments efficiently.
- Two clients may see the same slot; persistence must allow at most one conflicting booking to succeed.
- The duration and time-zone policies must be defined before implementing slot calculation.

## Revisit When

Slots acquire independent business state, temporary holds are required, or measured read performance justifies a different representation.

## References

- [Domain Model — DoctorAvailability](../DomainModel.md#9-doctoravailability)
- [ERD](../ERD.md)
- [Concurrency and Atomic Rescheduling](0005-scheduling-concurrency.md)
