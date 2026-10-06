# Hospital Platform — Business Rules

## 1. Purpose

This document defines the business rules and invariants that govern Version 1 of the Hospital Platform.

Business rules describe conditions that must remain true regardless of which client initiates an operation.

These rules apply equally to requests originating from:

- the React web application;
- the React Native mobile application;
- hospital staff interfaces;
- direct API clients.

The backend API is responsible for enforcing these rules.

Where appropriate, critical rules should also be protected through database constraints, transactions, or concurrency mechanisms.

The rules defined in this document will serve as a foundation for future:

- domain validation;
- application validation;
- database constraints;
- unit tests;
- integration tests;
- concurrency tests.

---

## 2. Rule Conventions

Business rule identifiers follow the format:

`BR-{MODULE}-{NUMBER}`

Modules used in this document:

- `GEN` — General System Rules
- `ACC` — Account Management
- `PAT` — Patient Management
- `VER` — Patient Identity Verification
- `DOC` — Doctor Management
- `SPEC` — Medical Specialties
- `AVL` — Doctor Availability
- `APT` — Appointment Management
- `REC` — Receptionist Operations
- `ADM` — Administration

---

# 3. General System Rules

## BR-GEN-001 — Backend Authority

The backend API shall be the authoritative source for business-rule enforcement.

Client-side validation may improve the user experience but shall never replace backend validation.

---

## BR-GEN-002 — Authorization Enforcement

Every protected operation shall validate that the authenticated user has the required permissions before the operation is executed.

---

## BR-GEN-003 — Resource Ownership

Users shall only access resources they own or resources explicitly permitted by their assigned role.

---

## BR-GEN-004 — Invalid State Protection

The system shall reject any operation that would leave a domain entity in an invalid state.

---

## BR-GEN-005 — Persistent Business State

Critical business state shall not depend on the memory of an individual API instance.

Persistent or shared state shall be stored in an appropriate external system such as PostgreSQL or Redis.

PostgreSQL shall remain the authoritative source for critical persistent business information.

---

# 4. Account Rules

## BR-ACC-001 — Unique Email

A user email address shall identify at most one user account.

Two user accounts shall not share the same email address.

---

## BR-ACC-002 — User Role

Every User account shall have exactly one Patient, Doctor, or Staff profile, linked through UserId.

User.ProfileType shall match the profile and shall be Patient, Doctor, or Staff. This is separate from StaffRole. Database type constraints, composite foreign keys, and unique UserId shall enforce at most one matching profile; deferred constraint triggers shall reject committed states with no profile, as defined in [ADR-0008](adr/0008-single-user-profile.md).

Patient and Doctor authorization roles correspond to their profile types. Receptionist and Administrator are StaffRole values on Staff, not separate profile entities. Each Staff profile shall have exactly one StaffRole.

Version 1 roles are:

- Patient
- Doctor
- Receptionist
- Administrator

---

## BR-ACC-003 — Active Account Requirement

A user must have an account state that permits system access before performing protected operations.

---

## BR-ACC-004 — Suspended Account

A suspended account shall not be allowed to perform protected operations.

Suspension does not imply that previously stored user information is removed.

---

## BR-ACC-005 — Deactivated Account

A deactivated account shall not be allowed to perform protected operations.

---

## BR-ACC-006 — Pending Verification

The `PendingVerification` status shall primarily represent patient accounts that have not yet completed the required identity verification process.

These patients shall retain limited access to their account information and verification instructions, without permission to book or reschedule appointments. The exact permission policy remains to be defined.

---

## BR-ACC-007 — Account Status and Identity Verification

Account status and patient identity verification shall be treated as separate concepts.

AccountStatus belongs to User. Identity verification attributes belong to Patient.

A previously verified patient may still have an account status such as:

- `Suspended`;
- `Deactivated`.

Identity verification alone shall therefore not imply that the account is currently permitted to perform protected operations.

---

# 5. Patient Rules

## BR-PAT-001 — Self-Registration Status

A patient created through public self-registration shall initially have a `PendingVerification` account status.

---

## BR-PAT-002 — Unique National Identification

A national identification number shall identify at most one patient within the system.

---

## BR-PAT-003 — Appointment Access Requirement

A patient shall only be allowed to book or reschedule appointments when:

- their identity has been successfully verified; and
- their account is currently active.

---

## BR-PAT-004 — Own Patient Information

Patients shall only be allowed to access patient-specific information associated with their own account unless another explicit authorization rule applies.

---

## BR-PAT-005 — Staff-Created Patient

When a patient is registered in person by a receptionist, the patient may be created as verified and active only if the receptionist completes the required identity verification during registration.

Otherwise, the patient shall remain pending verification.

---

# 6. Patient Identity Verification Rules

## BR-VER-001 — In-Person Verification

Patient identity verification shall require the patient to complete the verification process in person at the hospital.

---

## BR-VER-002 — Authorized Verification

Only an authorized receptionist shall be allowed to complete the patient identity verification process.

---

## BR-VER-003 — Verification Audit Data

A successfully verified patient shall have sufficient audit information to identify:

- when the verification occurred;
- which authorized user performed the verification.

These shall be recorded as Patient.IdentityVerifiedAt and Patient.IdentityVerifiedByUserId. Both attributes shall be absent before verification and present after successful verification.

---

## BR-VER-004 — Verification Timestamp

A verification timestamp shall only be recorded when identity verification has been successfully completed.

---

## BR-VER-005 — Verification Actor

A verified patient record shall identify the authorized user responsible for the verification.

IdentityVerifiedByUserId shall reference the verifying User, who must have a Staff profile with StaffRole.Receptionist and be authorized at the time of verification.

---

## BR-VER-006 — Activation After Verification

A self-registered patient's account may transition from `PendingVerification` to `Active` only after successful identity verification.

Verification shall not automatically reactivate a Suspended or Deactivated account.

---

## BR-VER-007 — Booking Before Verification

An unverified patient shall not be allowed to book an appointment.

---

## BR-VER-008 — Rescheduling Before Verification

An unverified patient shall not be allowed to reschedule an appointment.

---

## BR-VER-009 — Verification Persistence

A patient's identity verification status shall remain conceptually independent from future account suspension or deactivation.

For example:

populated `IdentityVerifiedAt` and `IdentityVerifiedByUserId` attributes

may coexist with:

`AccountStatus = Suspended`

Verification status shall be determined from these attributes rather than a separate persisted boolean.

---

# 7. Doctor Rules

## BR-DOC-001 — Doctor Account Creation

Doctor accounts shall only be created by authorized administrators.

Public doctor registration shall not be supported.

The Doctor profile shall contain UserId, EmployeeNumber, and MedicalLicenseNumber. EmployeeNumber shall identify at most one employee across Doctor and Staff profiles. Its ownership shall be centralized in EmployeeNumbers and match the profile's UserId. Account, number registration, and profile creation shall be atomic. The MedicalLicenseNumber uniqueness policy remains to be defined.

---

## BR-DOC-002 — Doctor Specialty Assignment

A doctor may be associated with multiple medical specialties.

---

## BR-DOC-003 — Duplicate Specialty Assignment

The same medical specialty shall not be assigned to the same doctor more than once.

---

## BR-DOC-004 — Doctor Availability Ownership

A doctor shall only manage availability associated with their own doctor profile unless another explicit administrative rule permits otherwise.

---

## BR-DOC-005 — Doctor Schedule Access

A doctor shall only access appointment information associated with their own schedule unless another authorization rule explicitly permits access.

---

## BR-DOC-006 — Appointment Patient Information

A doctor may access the patient information required for appointments assigned to that doctor.

Access to unrelated patient information shall not be granted through the doctor role.

---

# 8. Medical Specialty Rules

## BR-SPEC-001 — Specialty Assignment

A medical specialty may be assigned to multiple doctors.

A doctor may also have multiple medical specialties.

---

## BR-SPEC-002 — Duplicate Relationship

A doctor-specialty relationship shall be unique.

The same doctor and specialty combination shall not exist more than once.

---

## BR-SPEC-003 — Specialty Administration

Medical specialties shall only be created or administratively modified by authorized administrators.

---

# 9. Doctor Availability Rules

## BR-AVL-001 — Valid Time Range

A doctor availability period must have:

`EndTime > StartTime`

An availability period with an equal or earlier end time shall be invalid.

---

## BR-AVL-002 — Past Availability

A new availability period shall not be created entirely in the past.

---

## BR-AVL-003 — Overlapping Availability

Two availability periods belonging to the same doctor shall not overlap.

---

## BR-AVL-004 — Availability Ownership

Availability shall always belong to exactly one doctor.

---

## BR-AVL-005 — Appointment Within Availability

An appointment shall fit completely inside a valid doctor availability period.

For an appointment with:

`AppointmentStart`

and:

`AppointmentEnd`

the selected availability must satisfy:

`AvailabilityStart <= AppointmentStart`

and:

`AppointmentEnd <= AvailabilityEnd`

---

## BR-AVL-006 — Existing Appointment Protection

A doctor shall not modify or remove an availability period in a way that invalidates an existing scheduled appointment.

The operation shall be rejected unless the affected appointment is first handled through a valid appointment workflow.

---

## BR-AVL-007 — Occupied Slot

A time interval occupied by an active scheduled appointment shall not be presented as available for another appointment with the same doctor.

---

## BR-AVL-008 — Cancelled Appointment

A cancelled appointment shall no longer block its previous time slot when the slot remains valid according to the doctor's availability.

---

## BR-AVL-009 — Available Slot Calculation

Available appointment slots shall be calculated using:

- doctor availability;
- appointment duration;
- existing scheduled appointments;
- applicable scheduling rules.

Slots shall be calculated dynamically and shall not be persisted as separate entities in Version 1. Each slot shall last 30 minutes and follow the hospital-local half-hour grid defined in ADR-0009.

---

## BR-AVL-010 — Hospital Operating Window

Availability periods shall fit within 09:00–18:00 on one America/Montevideo local date, Monday through Friday. Only complete grid-aligned slots within doctor availability may be offered. Saturday and Sunday shall not be bookable.

The window allows at most 18 slots per doctor on a fully available operating day before bookings are excluded. It shall not imply that the doctor works the full window. Holiday rules remain pending.

---

# 10. Appointment Rules

## BR-APT-001 — Verified Patient Requirement

A patient must be both:

- identity verified; and
- active

before creating an appointment.

---

## BR-APT-002 — Receptionist Booking

A receptionist may create an appointment on behalf of a patient only when that patient satisfies the requirements for appointment booking.

---

## BR-APT-003 — Future Appointment

A new appointment shall not be scheduled in the past.

---

## BR-APT-004 — Valid Doctor Availability

An appointment shall only be scheduled during a valid availability period belonging to the selected doctor.

EndTime shall be later than StartTime. Each Appointment shall reference PatientId, DoctorId, and MedicalSpecialtyId directly, rather than DoctorSpecialtyId. Booking and rescheduling shall validate that the selected doctor is assigned to the selected specialty.

---

## BR-APT-005 — Doctor Appointment Conflict

A doctor shall not have multiple scheduled appointments whose time intervals overlap.

---

## BR-APT-006 — Double Booking Prevention

A single doctor time slot shall not be successfully reserved by more than one appointment.

---

## BR-APT-007 — Concurrent Booking

When multiple requests attempt to reserve the same doctor time slot concurrently, at most one request shall successfully create the appointment.

This rule must remain true even when requests are processed by different API instances.

---

## BR-APT-008 — Appointment Status

Version 1 appointments shall support the following states:

- `Scheduled`
- `Cancelled`
- `Completed`

New appointments shall start as Scheduled.

---

## BR-APT-009 — Scheduled Appointment Transitions

A `Scheduled` appointment may transition to:

- `Cancelled`;
- `Completed`.

---

## BR-APT-010 — Cancelled Appointment Finality

A `Cancelled` appointment shall not transition back to:

- `Scheduled`;
- `Completed`.

---

## BR-APT-011 — Completed Appointment Finality

A `Completed` appointment shall not transition back to:

- `Scheduled`;
- `Cancelled`.

---

## BR-APT-012 — Completion Authority

A doctor shall only mark an appointment as completed when the appointment belongs to that doctor's schedule.

---

## BR-APT-013 — Cancellation by Patient

A patient may only cancel an appointment associated with their own patient account.

---

## BR-APT-014 — Cancellation by Doctor

A doctor may only cancel an appointment associated with their own schedule.

---

## BR-APT-015 — Cancellation by Receptionist

An authorized receptionist may cancel an appointment on behalf of a patient.

Cancellation by a patient, doctor, or receptionist shall only apply to Scheduled appointments and shall preserve the appointment record.

---

## BR-APT-016 — Rescheduling Eligibility

Only a future `Scheduled` appointment may be rescheduled.

---

## BR-APT-017 — Rescheduling Validation

The new time selected during rescheduling shall satisfy the same business rules as a newly created appointment.

This includes:

- doctor availability;
- appointment timing;
- booking permissions;
- patient identity verification and Active account status;
- doctor-specialty assignment;
- scheduling conflicts;
- concurrency protection.

---

## BR-APT-018 — Atomic Rescheduling

Appointment rescheduling shall behave as a single logical operation.

If the new time slot cannot be successfully reserved, the existing appointment shall remain unchanged.

The system shall not cancel or invalidate the original appointment before successfully securing the replacement time.

Rescheduling shall update StartTime, EndTime, and UpdatedAt on the existing row in one transaction, preserving Id, PatientId, DoctorId, MedicalSpecialtyId, CreatedAt, and Scheduled status. Any failed operation shall roll back and leave the original row unchanged.

The appointment shall be excluded from its own conflict check. Concurrent rescheduling, cancellation, or completion shall be coordinated using the current appointment state, as defined in [ADR-0010](adr/0010-rescheduled-existing-appointment.md).

---

## BR-APT-019 — Appointment Ownership

A patient shall only retrieve appointment details associated with their own patient account.

---

## BR-APT-020 — Doctor Appointment Access

A doctor shall only retrieve detailed appointment information belonging to their own schedule unless explicitly authorized otherwise.

---

## BR-APT-021 — Cancelled Slot Reuse

When an appointment is cancelled, its previous slot may become available for booking again if:

- the slot remains in the future;
- it is still covered by valid doctor availability;
- no other scheduling rule prevents booking.

---

## BR-APT-022 — Appointment Duration and Time Policy

Each appointment shall last exactly 30 minutes. Booking and rescheduling shall use starts on the half-hour grid anchored at 09:00 in America/Montevideo, with no seconds or fractional seconds.

The first possible start is 09:00; the last is 17:30, ending at 18:00. The interval shall fit within the operating window and valid doctor availability on the same local date, Monday through Friday as evaluated in America/Montevideo. An 18:00 start is invalid.

Timestamp instants shall be interpreted consistently regardless of the client, API host, or database session's default time zone. See [ADR-0009](adr/0009-appointment-time-policy.md).

---

# 11. Receptionist Rules

## BR-REC-001 — Receptionist Role

Only users authorized as receptionists shall perform receptionist-specific operations.

These users shall have a Staff profile with StaffRole.Receptionist.

---

## BR-REC-002 — Patient Verification

Receptionists may perform identity verification only through the defined verification workflow.

---

## BR-REC-003 — Patient Appointment Operations

A receptionist may:

- create;
- cancel;
- reschedule

appointments on behalf of patients according to the same domain rules that apply to the corresponding operation.

---

## BR-REC-004 — No Administrative Privileges

The receptionist role shall not implicitly grant administrator privileges.

---

# 12. Administrator Rules

## BR-ADM-001 — Administrative Access

Administrative operations shall only be performed by authorized administrators.

These users shall have a Staff profile with StaffRole.Administrator.

---

## BR-ADM-002 — Doctor Management

Only authorized administrators shall create doctor accounts.

---

## BR-ADM-003 — Staff Management

Only authorized administrators shall create or administratively manage staff accounts for receptionists and administrators.

Each Staff profile shall contain UserId, EmployeeNumber, and exactly one StaffRole. EmployeeNumber shall identify at most one employee across Doctor and Staff profiles. Version 1 shall not introduce a shared Employee entity.

EmployeeNumbers shall register the number for the same User as the Staff profile. Account, number registration, and profile creation shall be atomic. Account deactivation shall not release the number registration.

---

## BR-ADM-004 — Account Status Management

Authorized administrators may manage eligible account statuses according to system rules.

Administrative status changes shall respect patient verification requirements. The complete status-transition policy remains to be defined.

---

## BR-ADM-005 — Medical Specialty Management

Only authorized administrators shall create or administratively modify medical specialties.

---

## BR-ADM-006 — Doctor Specialty Management

Only authorized administrators shall assign or remove medical specialties from doctors.

---

# 13. Concurrency Rules

## BR-CON-001 — Booking Consistency

Appointment booking consistency shall not depend exclusively on application-level checks performed before persistence.

The persistence layer shall provide sufficient protection to prevent conflicting bookings under concurrent requests.

---

## BR-CON-002 — Multiple API Instances

All concurrency guarantees shall remain valid when multiple backend API instances process requests simultaneously.

---

## BR-CON-003 — Cache Independence

Redis or any other cache shall not be used as the authoritative mechanism for determining whether an appointment booking is valid.

Critical appointment consistency shall be enforced against authoritative persistent data.

---

# 14. Business Rule Testing Strategy

Each critical business rule should later be represented by one or more automated tests.

The appropriate test type depends on the rule.

### Unit Tests

Unit tests should cover rules that can be evaluated without infrastructure.

Examples:

- invalid appointment state transitions;
- invalid availability time ranges;
- verification requirements;
- account status restrictions.

### Integration Tests

Integration tests should cover rules that depend on persistence or multiple application components.

Examples:

- unique patient national identification;
- unique user email;
- duplicate doctor-specialty assignments;
- appointment persistence;
- availability conflicts.

### Concurrency Tests

Concurrency-sensitive rules require integration tests capable of issuing simultaneous operations.

Examples:

- `BR-APT-006` — Double Booking Prevention;
- `BR-APT-007` — Concurrent Booking;
- `BR-CON-001` — Booking Consistency.

A successful concurrency test should demonstrate that when multiple requests attempt to reserve the same slot:

- exactly one request may succeed;
- conflicting requests are rejected;
- only one valid appointment exists in the database afterward.

---

# 15. Traceability

Business rules should remain traceable to functional requirements and future automated tests.

Example:

`FR-APT-006 — Concurrent Booking`

↓

`BR-APT-006 — Double Booking Prevention`

`BR-APT-007 — Concurrent Booking`

`BR-CON-001 — Booking Consistency`

↓

Future integration/concurrency tests

This traceability should make it possible to understand why a specific validation, constraint, or test exists within the system.

---

# 16. Rule Enforcement Principle

A business rule should be enforced at the lowest appropriate layer without unnecessarily duplicating domain logic.

Depending on the rule, enforcement may involve:

- domain logic;
- application services;
- authorization policies;
- database constraints;
- transactions;
- concurrency control.

Critical invariants should not rely solely on client-side checks or cached data.

The objective is not only to reject invalid requests, but to ensure that the system cannot persist an invalid business state.
