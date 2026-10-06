# ADR-0009 — Fixed Appointment Duration and Hospital Hours

## Status

Accepted for Version 1. Holiday rules remain pending.

## Context

Dynamic slot calculation needs a defined appointment duration and a consistent interpretation of hospital hours across web, mobile, and API instances.

Version 1 assumes one hospital in Uruguay with appointments Monday through Friday during a 09:00–18:00 operating window.

## Decision

- All Version 1 appointments last exactly 30 minutes.
- Interpret hospital hours and scheduling dates in America/Montevideo (where I live).
- Availability periods must fit within 09:00–18:00 on one hospital-local date, Monday through Friday. Saturday and Sunday are not bookable.
- Appointment starts follow a 30-minute grid anchored at 09:00, with no seconds or fractional seconds.
- The first possible start is 09:00; the last is 17:30, ending at 18:00. Starting at 18:00 is invalid.
- Each appointment must fit entirely within a doctor's availability, be in the future, and satisfy the existing booking rules.
- Apply the same duration, grid, and operating-window rules to booking and rescheduling.

Use PostgreSQL timestamptz for instants and exchange API timestamps as ISO 8601 values with UTC or an explicit offset. Convert to America/Montevideo when evaluating local business hours and displaying hospital schedules. Do not depend on the API host, browser, or database session's default time zone.

Retain the named hospital time zone separately from stored instants; timestamptz does not preserve the original named zone.

A doctor available continuously from 09:00 to 18:00 has 18 potential slots before existing appointments are excluded. This is a capacity ceiling, not an automatically generated daily schedule. Shorter periods, breaks, and occupied intervals reduce availability. For periods with boundaries between grid points, return only complete grid-aligned slots that fit within the period.

Evaluate the weekday in America/Montevideo, not from the UTC date or the client's time zone. Holiday rules must still be decided before implementing the scheduling calendar; this decision does not introduce a holiday calendar.

## Alternatives Considered

- Variable duration by doctor or specialty: supports more scheduling patterns, but adds rules not required for Version 1.
- Arbitrary appointment start times: allows greater flexibility, but does not match the agreed half-hour schedule.
- Server-local time or offset-free timestamps: can produce different interpretations across clients and deployments.
- Persist every daily slot: adds synchronization work without changing the time rules and conflicts with ADR-0004.

## Consequences

- One duration and grid simplify slot calculation and validation.
- The operating window alone does not establish a doctor's availability.
- Backend validation must reject off-grid starts, invalid duration, and intervals outside local hospital hours.
- Tests must cover 09:00, 17:30–18:00, rejection of an 18:00 start, off-grid starts, partial availability, occupied slots, Monday/Friday acceptance, Saturday/Sunday rejection, and equivalent timestamps expressed with different offsets.
- Duration customization and extended opening hours would require a revised policy.

## Revisit When

Different specialties require different durations, hospital hours change, or another hospital/time zone is introduced.

## References

- [Dynamic Appointment Slots](0004-dynamic-appointment-slots.md)
- [Domain Model](../DomainModel.md)
- [ERD](../ERD.md)
- [PostgreSQL date/time types](https://www.postgresql.org/docs/current/datatype-datetime.html)
