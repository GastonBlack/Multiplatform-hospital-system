# Hospital Platform — Domain Model

## 1. Purpose

This document defines the conceptual domain model for Version 1 of the Hospital Platform.

It identifies the main entities, their responsibilities, relationships, states, and invariants before database design and application implementation.

It does not define:

- database tables or constraints;
- Entity Framework Core mappings;
- C# inheritance;
- API request and response models;
- infrastructure implementation details.

---

## 2. Domain Overview

The core domain is appointment scheduling between verified patients and doctors.

The main entities are:

- User
- Patient
- Doctor
- Staff
- MedicalSpecialty
- DoctorAvailability
- Appointment

Supporting concepts are:

- ProfileType
- AccountStatus
- StaffRole
- AppointmentStatus
- Patient Identity Verification
- Available Appointment Slots

The conceptual user structure is:

```text
User
├── Patient
├── Doctor
└── Staff
    └── StaffRole
        ├── Receptionist
        └── Administrator
```

This structure represents a shared account associated with a specific profile. It does not prescribe inheritance or a database mapping strategy.

In Version 1, every User has exactly one profile: Patient, Doctor, or Staff.

Receptionist and Administrator are Staff roles, not separate profile entities.

---

## 3. User

### Responsibility

User represents the account used for authentication and system access.

It contains information shared by all account types.

### Conceptual Attributes

- Id
- FirstName
- LastName
- Email
- PasswordHash
- ProfileType
- AccountStatus
- CreatedAt
- UpdatedAt

### Relationships

Each User is associated with exactly one:

- Patient profile; or
- Doctor profile; or
- Staff profile.

### Invariants

- Email identifies at most one User.
- Passwords are never stored in plaintext.
- Each User has exactly one profile type in Version 1.
- ProfileType is Patient, Doctor, or Staff and must match the associated profile. It is distinct from StaffRole.
- AccountStatus belongs to User rather than being duplicated in each profile.
- Suspended and deactivated accounts cannot perform protected operations.

### ProfileType

Database enforcement of profile exclusivity and existence is defined in [ADR-0008](adr/0008-single-user-profile.md). Fixed profile-table discriminators are persistence details rather than additional editable profile attributes.

### Authorization

The system distinguishes four authorization roles:

- Patient, associated with a Patient profile;
- Doctor, associated with a Doctor profile;
- Receptionist, associated with StaffRole.Receptionist;
- Administrator, associated with StaffRole.Administrator.

Validated JWTs identify User.Id through sub and the authentication session through sid. Permissions are evaluated from current account/profile data rather than a token's role snapshot, as defined in [ADR-0012](adr/0012-authentication-sessions.md).

---

## 4. AccountStatus

AccountStatus describes the current access state of a User account.

Version 1 supports:

- PendingVerification
- Active
- Suspended
- Deactivated

### PendingVerification

A self-registered patient account starts in this state.

The patient must complete in-person identity verification before booking or rescheduling appointments.

This state allows login, session refresh, logout, and read-only access to the patient's own account/profile, account and identity-verification status, and verification instructions. All other protected patient operations, including profile updates and appointment management, are denied. Public specialty and doctor information remains accessible.

### Active

The account may perform operations permitted by its profile and role.

For patients, booking and rescheduling additionally require successful identity verification.

### Suspended

The account cannot perform protected operations.

Suspension does not remove the profile or its stored information.

### Deactivated

The account cannot perform protected operations.

Deactivation does not imply deletion of historical information.

### State Rules

- Successful identity verification allows an eligible patient account to transition from PendingVerification to Active.
- Identity verification does not automatically reactivate a Suspended or Deactivated account.
- Administrative account-status changes must respect patient verification requirements.
- The complete administrative transition policy remains to be defined.

---

## 5. Patient

### Responsibility

Patient represents a person who receives appointments at the hospital.

Authentication and shared personal information belong to the associated User.

### Conceptual Attributes

- Id
- UserId
- NationalIdentificationNumber
- PhoneNumber
- DateOfBirth
- IdentityVerifiedAt
- IdentityVerifiedByUserId

### Relationships

- Each Patient belongs to exactly one User.
- A Patient may have zero or more Appointments.
- IdentityVerifiedByUserId identifies the User who completed verification.

### Invariants

- NationalIdentificationNumber identifies at most one Patient.
- IdentityVerifiedAt and IdentityVerifiedByUserId are both absent before verification.
- Both verification attributes are present after successful verification.
- The verification actor must be an authorized receptionist at the time of verification.
- Booking and rescheduling require both successful verification and an Active account.
- Patients may only access their own patient information unless an explicit authorization rule permits otherwise.

### Identity Verification

Identity verification is separate from AccountStatus.

A verified patient may later have a Suspended or Deactivated account without losing the verification record.

A separate persisted IdentityVerified boolean is unnecessary for this model: verification can be determined from the verification attributes.

A receptionist may register a patient as verified and active when identity verification is completed during in-person registration. Otherwise, the account remains PendingVerification.

---

## 6. Doctor

### Responsibility

Doctor represents a hospital physician who provides appointments and manages availability.

### Conceptual Attributes

- Id
- UserId
- EmployeeNumber
- MedicalLicenseNumber

### Relationships

- Each Doctor belongs to exactly one User.
- A Doctor may be associated with multiple MedicalSpecialties.
- A Doctor may have zero or more DoctorAvailability periods.
- A Doctor may have zero or more Appointments.

### Invariants

- Doctor accounts are created by authorized administrators.
- Public doctor registration is not supported.
- EmployeeNumber identifies an employee across both Doctor and Staff profiles.
- The same specialty cannot be assigned to a doctor more than once.
- An assignment cannot be removed while the doctor has future Scheduled appointments for that specialty. Reception must cancel affected appointments first with a patient-visible reason and the existing in-app guidance. Removal preserves historical appointments and does not automatically cancel any appointment.
- Doctors manage their own availability and appointments according to authorization rules.
- Doctors may access the basic patient information required for appointments assigned to them.

---

## 7. Staff

### Responsibility

Staff represents an internal hospital user who performs reception or administrative operations.

### Conceptual Attributes

- Id
- UserId
- EmployeeNumber
- StaffRole

### Relationships

Each Staff profile belongs to exactly one User.

### Invariants

- Staff accounts are created and managed by authorized administrators.
- EmployeeNumber identifies an employee across both Doctor and Staff profiles.
- Each Staff profile has exactly one StaffRole in Version 1.
- Receptionist permissions do not implicitly include administrator permissions.

### StaffRole

Version 1 supports:

- Receptionist
- Administrator

Receptionists perform patient registration, identity verification, and appointment operations on behalf of patients.

Administrators manage doctor and staff accounts, account statuses, medical specialties, and doctor-specialty assignments.

Receptionist and Administrator are not separate entities.

### Employee Modeling

Version 1 does not introduce a shared Employee entity.

Doctor and Staff each contain EmployeeNumber.

Global EmployeeNumber ownership is centralized in a technical EmployeeNumbers registry at the persistence layer, as documented in the [ERD](ERD.md) and [ADR-0007](adr/0007-employee-number-registry.md). This does not introduce an Employee domain entity or change the profile attributes.

---

## 8. MedicalSpecialty

### Responsibility

MedicalSpecialty represents a medical field that patients can select when searching for doctors and booking appointments.

### Conceptual Attributes

- Id
- Name
- IsActive

IsActive represents whether the specialty is enabled for new selections.

### Relationships

- A MedicalSpecialty may be associated with multiple Doctors.
- A MedicalSpecialty may be referenced by multiple Appointments.

Doctor and MedicalSpecialty have a many-to-many relationship.

### Invariants

- Specialty management is restricted to authorized administrators.
- Each doctor-specialty combination is unique.
- Booking must validate that the selected doctor is assigned to the selected specialty.

The doctor-specialty association does not need to be a separate domain entity in Version 1. Its persistence representation will be defined in the ERD.

---

## 9. DoctorAvailability

### Responsibility

DoctorAvailability represents a time period during which a doctor offers appointments.

It represents an availability period rather than an individual bookable slot.

### Conceptual Attributes

- Id
- DoctorId
- StartTime
- EndTime

### Relationships

Each DoctorAvailability period belongs to exactly one Doctor.

### Invariants

- EndTime must be later than StartTime.
- A new availability period cannot be entirely in the past.
- Availability periods for the same doctor cannot overlap.
- A newly booked or rescheduled appointment must fit completely within a valid availability period.
- Availability cannot be modified or removed in a way that invalidates existing Scheduled appointments.

### Available Slots

Available slots are calculated dynamically from:

- doctor availability;
- the applicable appointment duration;
- existing Scheduled appointments;
- scheduling rules.

For example, a 09:00–11:00 availability period with a 30-minute appointment duration can produce:

```text
09:00–09:30
09:30–10:00
10:00–10:30
10:30–11:00
```

An occupied interval is excluded from the available results.

Available slots are not persisted as separate entities in Version 1.

Version 1 appointments last exactly 30 minutes. Slots follow the hospital-local half-hour grid from 09:00 through 17:30, with the last slot ending at 18:00. Use America/Montevideo for hospital scheduling dates and hours.

Availability periods must fit within 09:00–18:00 on one hospital-local date, Monday through Friday. Saturday and Sunday are not bookable. A continuous full-day period offers at most 18 slots per doctor before bookings are excluded. Shorter periods and breaks reduce this capacity; only complete grid-aligned slots inside availability may be returned.

Weekdays are evaluated in America/Montevideo. Version 1 has no holiday calendar or automatic holiday exclusion. Doctors omit availability on concrete dates when they will not attend; dates without availability have no bookable slots. A weekday holiday with valid availability remains bookable.

If existing appointments are affected by a later absence, reception cancels them through the normal workflow with a patient-visible reason and in-app guidance before availability is removed or changed. See [ADR-0009](adr/0009-appointment-time-policy.md).

---

## 10. Appointment

### Responsibility

Appointment represents a scheduled meeting between a patient and a doctor for a selected medical specialty.

### Conceptual Attributes

- Id
- PatientId
- DoctorId
- MedicalSpecialtyId
- StartTime
- EndTime
- Status
- CancellationReason
- CreatedAt
- UpdatedAt

### Relationships

Each Appointment references exactly one:

- Patient;
- Doctor;
- MedicalSpecialty.

Appointment references Doctor and MedicalSpecialty directly.

It does not reference a DoctorSpecialtyId.

The application validates that the doctor is assigned to the selected specialty when booking or rescheduling.

### Booking Invariants

- The patient must be identity verified.
- The patient's account must be Active.
- The doctor's User account must be Active and the selected MedicalSpecialty must be active.
- EndTime must be later than StartTime.
- The appointment cannot start in the past.
- The appointment must last exactly 30 minutes, start on the hospital's half-hour grid, and fit within 09:00–18:00 on one America/Montevideo local date, Monday through Friday.
- The appointment must fit completely within valid doctor availability.
- Scheduled appointments for the same doctor cannot overlap.
- These rules also apply when a receptionist books on behalf of a patient.

### AppointmentStatus

Version 1 supports:

- Scheduled
- Cancelled
- Completed

New appointments start as Scheduled.

Allowed transitions are:

```text
Scheduled → Cancelled
Scheduled → Completed
```

Cancelled and Completed are terminal states.

### Cancellation

- Patients may cancel their own appointments.
- Doctors may cancel appointments assigned to them.
- Authorized receptionists may cancel appointments on behalf of patients.
- Only Scheduled appointments may be cancelled.
- A cancelled appointment releases its interval for booking when the interval remains in the future and satisfies availability and scheduling rules.
- Cancellation preserves the appointment record.

CancellationReason is optional except when reception cancels an appointment because of doctor suspension/deactivation or specialty deactivation; that workflow requires a patient-visible explanation.

Such account or specialty changes block new bookings and rescheduling for the affected selection, but preserve availability and existing appointments. Reception reviews future Scheduled appointments and cancels those affected. There is no Suspended appointment state.

Patients can view the cancellation status and reason in appointment lists and details, including future cancelled appointments, with instructions to book another appointment or contact reception. These notices appear when the application loads or refreshes persisted data; email, SMS, and push remain outside Version 1. See [ADR-0011](adr/0011-service-interruption-cancellations.md).

### Completion

Only the doctor assigned to an appointment may mark it as Completed.

Only Scheduled appointments may be completed.

### Rescheduling

Only a future Scheduled appointment may be rescheduled.

The new interval must satisfy the same booking rules as a new appointment.

Rescheduling must be atomic:

- if the new interval is successfully reserved, the change is applied;
- if the operation fails, the original appointment remains unchanged.

The original appointment must not be lost because the replacement interval is unavailable.

Rescheduling updates StartTime and EndTime on the existing Appointment and updates UpdatedAt. Id, PatientId, DoctorId, MedicalSpecialtyId, CreatedAt, and Scheduled status remain unchanged. The operation changes time only; it does not cancel the original row or create a replacement.

The appointment being rescheduled must be excluded from its own conflict check. Concurrent changes to that appointment must be coordinated so stale requests cannot overwrite a cancellation, completion, or another time change.

Version 1 does not store a separate history of previous appointment intervals. See [ADR-0010](adr/0010-rescheduled-existing-appointment.md).

### Concurrency

Conflicting bookings must be prevented even when requests are processed simultaneously by different API instances.

Checking availability before insertion is not sufficient by itself.

PostgreSQL is the authoritative store for appointment consistency. [ADR-0005](adr/0005-scheduling-concurrency.md) selects transactions, coordinated row locks, and exclusion constraints. Writes reload current eligibility and scheduling data after locking. Appointment mutations reject an outdated expected UpdatedAt; successful changes persist a strictly newer value. Availability edits must preserve coverage of existing Scheduled appointments.

Redis is not the authority for booking validity.

---

## 11. Relationship Summary

| Relationship | Cardinality |
| --- | --- |
| User — profile | Exactly one Patient, Doctor, or Staff profile per User |
| Patient — User | Exactly one User per Patient |
| Doctor — User | Exactly one User per Doctor |
| Staff — User | Exactly one User per Staff |
| Doctor — MedicalSpecialty | Many-to-many |
| Doctor — DoctorAvailability | One-to-many |
| Patient — Appointment | One-to-many |
| Doctor — Appointment | One-to-many |
| MedicalSpecialty — Appointment | One-to-many |
| Verified Patient — verifying User | Exactly one recorded actor per verified Patient |

A User may verify multiple patients if authorized as a receptionist when performing each verification.

---

## 12. Decisions Still Required

The following decisions must be resolved in subsequent design work:

- implementation and concurrency validation of the exactly-one-profile persistence design in ADR-0008;
- EmployeeNumber normalization and generation policies;
- the uniqueness policy for MedicalLicenseNumber;
- the complete administrative AccountStatus transition policy;
- the persistence representation of doctor-specialty assignments;
- implementation and PostgreSQL concurrency validation of the scheduling protocol in ADR-0005.

These are open design decisions, not implemented guarantees.

---

## 13. Version 1 Boundaries

This model covers:

- user accounts;
- patient profiles and identity verification;
- doctor profiles;
- staff profiles and roles;
- medical specialties;
- doctor availability;
- appointment scheduling and lifecycle.

It does not include:

- payments or billing;
- insurance processing;
- complete electronic medical records;
- diagnoses or clinical notes;
- prescriptions;
- pharmacy operations;
- laboratory operations;
- hospital inventory;
- emergency department workflows;
- multiple hospital organizations.

AuthenticationSession and RefreshToken persistence is documented in [AuthenticationModel.md](AuthenticationModel.md), separately from the core scheduling domain.

---

## 14. Documentation Alignment

After this domain model is reviewed, the following documents must be aligned with it:

1. Scope.md
2. FunctionalRequirements.md
3. BusinessRules.md
4. NonFunctionalRequirements.md

Alignment must preserve the distinction between:

- User accounts and domain profiles;
- profile types and StaffRole;
- AccountStatus and patient identity verification;
- availability periods and calculated slots;
- conceptual domain relationships and database implementation.

Database design and implementation should follow this documentation review.
