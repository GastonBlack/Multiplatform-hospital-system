# ADR-0011 — Receptionist Review and In-App Cancellation Notices

## Status

Accepted for Version 1. Concurrent booking versus account or specialty changes still requires transactional implementation and tests.

## Context

A doctor's account may be Suspended or Deactivated, or a medical specialty may become inactive. Such changes block new bookings but do not necessarily invalidate every existing appointment.

Patients need to understand affected cancellations and know how to obtain another appointment. Email, SMS, and push delivery remain future functionality.

## Decision

- Require an Active doctor account and an active selected MedicalSpecialty for booking and rescheduling.
- Block new reservations for unavailable doctors or inactive specialties; do not offer their slots as bookable for the affected selection.
- Preserve existing appointments and doctor availability records when the account or specialty state changes. Do not automatically cancel, complete, or suspend appointments.
- Allow authorized receptionists to inspect the affected future Scheduled appointments and decide which must be cancelled.
- Cancel through the normal Scheduled → Cancelled workflow, preserving the row and storing a patient-visible CancellationReason.
- Display the cancellation status, reason, and instructions to book another appointment or contact reception in both patient clients' appointment lists and details. Future cancelled appointments must remain accessible rather than disappearing from the patient-facing view.

Appointment gains an optional text CancellationReason attribute. It is required when reception cancels an appointment through this interruption workflow; it does not change requirements for other cancellation paths. Use a patient-facing explanation, such as provider unavailability, rather than internal personnel details.

The in-app notice is derived from the persisted appointment data and shown when the patient loads or refreshes it. Version 1 does not promise real-time delivery, a separate notification inbox, or a read receipt.

Appointments retain Scheduled, Cancelled, and Completed states. Do not add a Suspended appointment state.

Rescheduling remains a time-only change under ADR-0010. Moving to another doctor or specialty requires cancellation and a separate new booking; these two actions are not an atomic transfer or a guarantee of replacement availability.

## Alternatives Considered

- Automatically cancel every affected future appointment: less manual work, but a temporary interruption may not affect every scheduled date.
- Add a Suspended appointment state: requires new transitions and recovery behavior without a current need.
- Preserve appointments without a visible explanation: leaves patients without clear guidance after cancellation.
- Email, SMS, or push delivery in Version 1: adds integrations beyond the agreed scope.

## Consequences

- Reception has control over which reservations are cancelled; pending review does not imply a confirmed interruption notice for the patient.
- Existing Scheduled appointments continue blocking their intervals until cancelled or completed.
- Both clients must expose cancellation information from the same authoritative data.
- A cancelled appointment may release its interval, but an unavailable doctor or inactive specialty still cannot receive a new reservation for the affected selection.
- Tests must cover booking rejection, unchanged appointments after state changes, authorized cancellation with a reason, visibility in patient clients, and preserved terminal states.

## Revisit When

Automatic cancellation, real-time notices, external delivery, or atomic transfers to another provider become required.

## References

- [Domain Model](../DomainModel.md)
- [Scope](../Scope.md)
- [In-Place Rescheduling](0010-rescheduled-existing-appointment.md)
- [Business Rules](../BusinessRules.md)
