# Hospital Platform — Functional Requirements

## 1. Purpose

This document defines the functional requirements for Version 1 of the Hospital Platform.

The requirements describe the behavior the system must provide to patients, doctors, receptionists, and administrators.

Each requirement is assigned a unique identifier so that it can later be referenced from:

- business rules;
- API endpoints;
- domain entities;
- automated tests;
- architecture documentation.

---

## 2. Requirement Conventions

Requirement identifiers follow the format:

`FR-{MODULE}-{NUMBER}`

Modules used in this document:

- `AUTH` — Authentication and Accounts
- `PAT` — Patient Management
- `VER` — Patient Identity Verification
- `DOC` — Doctor Management
- `REC` — Receptionist Operations
- `ADM` — Administration
- `SPEC` — Medical Specialties
- `AVL` — Doctor Availability
- `APT` — Appointment Management

The word **shall** indicates functionality required for Version 1.

---

# 3. Authentication and Account Management

## FR-AUTH-001 — User Authentication

The system shall allow registered users to authenticate using their credentials.

Email-based login shall use the same NormalizeEmail policy as account creation and permitted email changes, as defined in BR-ACC-001: trim exterior whitespace, convert to lowercase independently of server culture, and reject internal whitespace without removing it.

---

## FR-AUTH-002 — Invalid Credentials

The system shall reject authentication attempts when the supplied credentials are invalid.

---

## FR-AUTH-003 — Access Token

The system shall issue an access token after successful authentication.

The access token shall be a signed JWT with a maximum 15-minute lifetime, capped by session expiry. Validated sub and sid claims shall identify User.Id and the authentication session respectively.

---

## FR-AUTH-004 — Refresh Token

The system shall issue a refresh token after successful authentication.

---

## FR-AUTH-005 — Token Refresh

The system shall allow a valid refresh token to be exchanged for a new access token.

---

## FR-AUTH-006 — Refresh Token Rotation

The system shall rotate refresh tokens when they are successfully used.

The previously used refresh token shall no longer be valid.

Rotation shall atomically consume the previous token and persist its replacement within the same session, without extending the original session expiry. Reuse of a consumed token shall revoke that session; an unknown token shall be rejected without revoking an unrelated session.

---

## FR-AUTH-007 — Logout

The system shall allow authenticated users to terminate their current session by revoking the persistent authentication session and removing its client credentials. Subsequent protected requests shall reject that session's access and refresh tokens. Other independent sessions shall remain unaffected.

---

## FR-AUTH-008 — Role Assignment

Every User account shall have exactly one Patient, Doctor, or Staff profile, linked through UserId.

User.ProfileType shall identify Patient, Doctor, or Staff and match the associated profile. The system shall create the account and profile atomically and reject a transaction that would leave an existing User with no profile or multiple profiles, using the persistence design in [ADR-0008](adr/0008-single-user-profile.md).

Patient and Doctor authorization roles correspond to their profile types. A Staff profile shall have exactly one StaffRole: Receptionist or Administrator. These Staff roles shall not be modeled as separate profile entities.

Supported roles for Version 1 are:

- Patient
- Doctor
- Receptionist
- Administrator

---

## FR-AUTH-009 — Role-Based Access

The system shall restrict protected operations according to the authenticated user's role and permissions.

---

## FR-AUTH-010 — Account Status

The system shall maintain AccountStatus on User, independently of patient identity verification.

Supported account statuses in Version 1 shall be:

- `PendingVerification`
- `Active`
- `Suspended`
- `Deactivated`

PendingVerification patients shall be allowed to log in, refresh their session, log out, and read their own account/profile, account and identity-verification status, and verification instructions. All other protected patient operations, including profile updates and appointment management, shall be denied while this status remains in effect. Public specialty and doctor information remains accessible without granting protected permissions.

---

## FR-AUTH-011 — Suspended Accounts

The system shall prevent suspended users from performing protected operations.

Suspension shall revoke the User's sessions atomically with the AccountStatus change. Reactivation shall require a new login.

---

## FR-AUTH-012 — Deactivated Accounts

The system shall prevent deactivated users from performing protected operations.

Deactivation shall revoke the User's sessions atomically with the AccountStatus change. Reactivation shall require a new login.

---

## FR-AUTH-013 — Current User Information

The system shall allow an authenticated user to retrieve their own basic account and profile information.

---

## FR-AUTH-014 — Session Lifetime and Current Authorization

Each successful login shall create an independent session with an absolute 7-day lifetime, permitting separate web and mobile sessions for the same User. Token rotation shall not extend that deadline.

Every authenticated protected request shall validate the JWT and the current session's ownership, expiry, and revocation, plus current account/profile permissions in PostgreSQL. A valid JWT shall not override account restrictions, resource ownership, or patient identity-verification requirements.

Refresh requests shall coordinate session locking, eligibility validation, token consumption, replacement, and reuse revocation as defined in [ADR-0012](adr/0012-authentication-sessions.md).

---

# 4. Patient Management

## FR-PAT-001 — Patient Self-Registration

The system shall allow a person to create a patient account through public registration.

---

## FR-PAT-002 — Initial Patient Status

A patient account created through self-registration shall initially have a `PendingVerification` account status.

---

## FR-PAT-003 — Patient Information

Patient registration shall collect the information required to identify and manage the patient within the platform.

This shall include at least:

- first name;
- last name;
- email;
- password;
- national identification number;
- date of birth;
- phone number.

FirstName, LastName, Email, and PasswordHash belong to User. NationalIdentificationNumber, DateOfBirth, and PhoneNumber belong to the Patient profile.

---

## FR-PAT-004 — Unique Email

The system shall prevent multiple user accounts from being registered with the same email address.

All account creation and permitted email changes shall store canonical User.Email values using NormalizeEmail and PostgreSQL uniqueness enforcement. Exterior whitespace and letter case shall not distinguish accounts. Internal whitespace shall be rejected; dots and plus suffixes shall be preserved without provider-specific rewriting. Validate the address before persistence. This policy applies to Patient, Doctor, and Staff accounts.

---

## FR-PAT-005 — Unique National Identification

The system shall prevent multiple patient records from using the same national identification number.

Version 1 shall accept only Uruguayan cédulas de identidad including the supplied check digit. All registration paths and permitted identity-number changes/lookups shall use NormalizeNationalIdentificationNumber as defined in BR-PAT-002: remove presentation whitespace, dots, and hyphens, reject other non-digit characters, and preserve leading zeros and the final check digit. Store the canonical digit string as text and enforce uniqueness in PostgreSQL. Version 1 shall not calculate or validate the check-digit checksum. Format validation shall not replace receptionist identity verification.

The normalized input shall contain exactly eight ASCII digits (0–9), including the supplied check digit. The system shall reject any other length and request the complete cédula, including any leading zero and the check digit, rather than automatically padding or truncating it. Public and receptionist registration and permitted identity-number changes shall not persist invalid values.

---

## FR-PAT-006 — Patient Profile

An authenticated patient shall be able to view their own profile.

---

## FR-PAT-007 — Patient Profile Update

A patient shall be able to modify profile information that the system allows patients to manage directly.

This protected operation requires an Active account; PendingVerification access to the patient's own profile is read-only.

Information considered sensitive or related to verified identity may require hospital staff intervention.

---

## FR-PAT-008 — Verification Status

A patient shall be able to view their identity verification status separately from their User account status.

---

## FR-PAT-009 — Upcoming Appointments

A patient shall be able to view their upcoming appointments.

---

## FR-PAT-010 — Appointment History

A patient shall be able to view their past appointments.

---

# 5. Patient Identity Verification

## FR-VER-001 — In-Person Verification

A self-registered patient shall be required to complete an in-person identity verification process before gaining access to appointment booking and rescheduling functionality.

---

## FR-VER-002 — Receptionist Verification

The system shall allow an authorized receptionist to verify a patient's identity.

---

## FR-VER-003 — Patient Search for Verification

A receptionist shall be able to locate a patient requiring verification using appropriate patient information.

---

## FR-VER-004 — Verification Record

When a patient is successfully verified, the system shall record:

- Patient.IdentityVerifiedAt, containing the date and time of verification;
- Patient.IdentityVerifiedByUserId, identifying the authorized receptionist's User account.

Both attributes shall be absent before verification and present after successful verification. Verification status shall be determined from these attributes rather than a separate persisted boolean.

---

## FR-VER-005 — Account Activation

After successful in-person identity verification, the system shall allow only an authorized receptionist to transition an eligible patient's User account from `PendingVerification` to `Active`.

Verification shall not automatically reactivate a `Suspended` or `Deactivated` account.

---

## FR-VER-006 — Booking Restriction

The system shall reject appointment booking requests performed by a patient whose identity has not been successfully verified.

---

## FR-VER-007 — Rescheduling Restriction

The system shall reject appointment rescheduling requests performed by a patient whose identity has not been successfully verified.

---

## FR-VER-008 — Staff Registration and Verification

When a receptionist registers a patient during an in-person visit and verifies the patient's identity during the same process, the system shall allow the patient account to be created as verified and active.

---

## FR-VER-009 — Verification Audit Information

Authorized hospital staff shall be able to determine when and by whom a patient's identity was verified.

---

# 6. Doctor Management

## FR-DOC-001 — Doctor Creation

The system shall allow administrators to create doctor accounts.

Doctors shall not be able to register themselves publicly.

---

## FR-DOC-002 — Doctor Profile

The system shall maintain a Doctor profile linked to User through UserId, including EmployeeNumber and MedicalLicenseNumber.

EmployeeNumber shall be unique across Doctor and Staff profiles, with number ownership centralized in the EmployeeNumbers registry. Account, number registration, and profile creation shall be atomic.

The system shall assign EmployeeNumber automatically from the shared PostgreSQL sequence defined in [ADR-0007](adr/0007-employee-number-registry.md), using canonical EMP- plus at least six decimal digits. Clients shall not supply or edit it. Numbers shall not be reused after suspension/deactivation; sequence gaps are permitted. Only authorized hospital personnel shall receive employee numbers; public doctor data and patient responses shall omit them.

MedicalLicenseNumber shall be required and unique across all Doctor profiles, including those linked to Suspended or Deactivated accounts. Version 1 assumes a single medical-license numbering system. The system shall reject duplicate numbers when creating or updating a Doctor profile; account status changes shall not release the number for another profile.

---

## FR-DOC-003 — Doctor Status

The system shall allow administrators to activate, suspend, or deactivate doctor accounts.

Only doctors with Active User accounts shall accept new bookings or rescheduling. Suspension or deactivation shall preserve availability and existing appointments for receptionist review under FR-APT-025.

---

## FR-DOC-004 — Doctor Specialties

A doctor shall be able to have one or more medical specialties assigned.

---

## FR-DOC-005 — Doctor Specialty Visibility

A doctor shall be able to view the medical specialties assigned to them.

---

## FR-DOC-006 — Doctor Search

The system shall allow patients and authorized hospital staff to search for doctors.

---

## FR-DOC-007 — Doctor Filtering

The system shall allow doctors to be filtered by medical specialty.

---

## FR-DOC-008 — Doctor Information

The system shall allow users to view relevant public doctor information required to select a healthcare provider.

---

## FR-DOC-009 — Doctor Schedule

A doctor shall be able to view their appointment schedule.

---

## FR-DOC-010 — Patient Information for Appointment

A doctor shall be able to view the basic information of patients associated with their appointments.

Doctors shall not receive access to unrelated patient information.

---

# 7. Receptionist Operations

## FR-REC-001 — Patient Search

A receptionist shall be able to search for patients.

---

## FR-REC-002 — Patient Registration

A receptionist shall be able to register a patient.

---

## FR-REC-003 — Verification Management

A receptionist shall be able to view whether a patient requires identity verification.

---

## FR-REC-004 — Identity Verification

A receptionist shall be able to perform the patient identity verification process.

---

## FR-REC-005 — Doctor Schedule Access

A receptionist shall be able to view doctor schedules.

---

## FR-REC-006 — Available Slots

A receptionist shall be able to view available appointment slots for a doctor.

---

## FR-REC-007 — Appointment Creation for Patient

A receptionist shall be able to create an appointment on behalf of a verified patient.

---

## FR-REC-008 — Appointment Cancellation for Patient

A receptionist shall be able to cancel an appointment on behalf of a patient.

---

## FR-REC-009 — Appointment Rescheduling for Patient

A receptionist shall be able to reschedule an appointment on behalf of a verified patient.

---

## FR-REC-010 — Administrative Appointment Information

A receptionist shall be able to view the appointment information required to perform administrative operations.

---

# 8. Administrator Operations

## FR-ADM-001 — Doctor Account Management

An administrator shall be able to create and manage doctor accounts.

---

## FR-ADM-002 — Staff Account Management

An administrator shall be able to create and manage User accounts with Staff profiles for receptionists and administrators.

Each Staff profile shall contain UserId, EmployeeNumber, and exactly one StaffRole. EmployeeNumber shall be unique across Doctor and Staff profiles.

Account, EmployeeNumbers registration, and Staff profile creation shall be atomic. The registered number shall belong to the same User as the Staff profile.

Staff registration shall use the same automatic numbering, visibility, immutability, and non-reuse policy as Doctor registration, defined in FR-DOC-002 and ADR-0007.

---

## FR-ADM-003 — User Status Management

An administrator shall be able to change the status of eligible user accounts.

The system shall enforce BR-ADM-004: administrators may suspend/deactivate PendingVerification or Active accounts, change between Suspended and Deactivated, and reactivate eligible Suspended or Deactivated accounts to Active. Patient reactivation shall require completed identity verification; only receptionists shall perform the initial PendingVerification-to-Active activation after verification. The system shall reject other transitions, including a return to PendingVerification.

Suspension/deactivation shall revoke sessions atomically; reactivation shall require new login. Account-management operations shall not physically delete persisted accounts or profiles and shall preserve identifiers, verification records, employee registrations, and appointment history. Validate status transitions using current state and actor permissions under the ADR-0005 transaction and locks.

---

## FR-ADM-004 — Medical Specialty Management

An administrator shall be able to create and manage medical specialties.

---

## FR-ADM-005 — Specialty Assignment

An administrator shall be able to assign medical specialties to doctors.

---

## FR-ADM-006 — Specialty Removal

An administrator shall be able to remove a medical specialty assignment from a doctor.

The system shall reject removal while that doctor has future Scheduled appointments for that specialty, leaving the assignment unchanged. Reception shall cancel the affected appointments first, recording a patient-visible reason and the existing in-app guidance. Cancelled, Completed, and past appointments shall be preserved and shall not block removal. Validation and removal shall occur within the coordinated transaction in [ADR-0005](adr/0005-scheduling-concurrency.md).

---

## FR-ADM-007 — Doctor Information Management

An administrator shall be able to update administrative doctor information.

---

## FR-ADM-008 — Restricted Administrative Access

Administrator functionality shall only be accessible to users authorized to perform administrative operations.

---

# 9. Medical Specialties

## FR-SPEC-001 — Specialty Creation

The system shall allow administrators to create medical specialties.

---

## FR-SPEC-002 — Specialty Update

The system shall allow administrators to modify medical specialty information.

---

## FR-SPEC-003 — Specialty Status

The system shall allow medical specialties to be activated or deactivated.

Inactive specialties shall not accept new bookings or rescheduling. Existing appointments shall be preserved for receptionist review under FR-APT-025.

---

## FR-SPEC-004 — Specialty Listing

The system shall allow users to retrieve the available medical specialties.

---

## FR-SPEC-005 — Doctors by Specialty

The system shall allow users to retrieve doctors associated with a specific medical specialty.

---

## FR-SPEC-006 — Doctor Specialty Assignment

The system shall support the association of multiple medical specialties with a doctor.

---

## FR-SPEC-007 — Duplicate Specialty Assignment

The system shall prevent the same medical specialty from being assigned to the same doctor more than once.

---

# 10. Doctor Availability

## FR-AVL-001 — Availability Creation

A doctor shall be able to define periods during which they are available for appointments.

---

## FR-AVL-002 — Availability Modification

A doctor shall be able to modify future availability periods.

The system shall reject modifications that invalidate existing Scheduled appointments.

---

## FR-AVL-003 — Availability Removal

A doctor shall be able to remove future availability periods when doing so does not violate existing appointment rules.

---

## FR-AVL-004 — Valid Availability Range

The system shall reject availability periods whose end time is not later than their start time.

---

## FR-AVL-005 — Past Availability

The system shall reject the creation of availability periods entirely in the past.

---

## FR-AVL-006 — Overlapping Availability

The system shall prevent overlapping availability periods for the same doctor.

---

## FR-AVL-007 — Available Slot Calculation

The system shall calculate bookable appointment slots based on:

- doctor availability;
- appointment duration;
- existing Scheduled appointments;
- applicable scheduling rules.

Slots shall be calculated dynamically rather than persisted as separate entities in Version 1, using the 30-minute duration and half-hour hospital grid defined in ADR-0009.

---

## FR-AVL-008 — Occupied Slots

A time interval occupied by a Scheduled appointment shall not be returned as available for another appointment with the same doctor.

---

## FR-AVL-009 — Availability Retrieval

Patients and authorized hospital staff shall be able to retrieve available appointment slots for a doctor.

---

## FR-AVL-010 — Hospital Operating Window

The system shall restrict availability to periods within 09:00–18:00 on one America/Montevideo local date, Monday through Friday. Slot calculation shall return only complete half-hour grid intervals that fit within valid availability and are not occupied by Scheduled appointments. Saturday and Sunday shall not be bookable.

Version 1 shall not introduce a holiday calendar or automatic holiday exclusions. Doctors shall create availability for concrete dates and omit dates when they will not attend. Dates without valid availability shall return no bookable slots; a weekday holiday with valid availability shall remain bookable. The operating window shall not imply full-day availability for every doctor.

If a later absence affects existing future Scheduled appointments, reception shall cancel the affected appointments with a patient-visible reason and in-app guidance before removing or changing availability that would invalidate them, according to [ADR-0009](adr/0009-appointment-time-policy.md).

---

# 11. Appointment Management

## FR-APT-001 — Appointment Creation

The system shall allow an identity-verified patient with an Active User account to book an available appointment.

Each appointment shall reference PatientId, DoctorId, and MedicalSpecialtyId directly, rather than DoctorSpecialtyId. The system shall validate that the selected doctor is assigned to the selected specialty and that EndTime is later than StartTime.

---

## FR-APT-002 — Appointment Creation by Receptionist

The system shall allow a receptionist to create an appointment on behalf of an identity-verified patient with an Active User account.

---

## FR-APT-003 — Doctor Availability Requirement

An appointment shall only be created within a valid availability period for the selected doctor.

---

## FR-APT-004 — Past Appointments

The system shall reject attempts to create appointments in the past.

---

## FR-APT-005 — Doctor Scheduling Conflict

The system shall prevent a doctor from having multiple Scheduled appointments that overlap in time.

---

## FR-APT-006 — Concurrent Booking

When multiple users attempt to book the same appointment slot concurrently, the system shall allow at most one booking to succeed.

Scheduling writes shall validate current data under the coordinated transaction and row-lock protocol in [ADR-0005](adr/0005-scheduling-concurrency.md). Rescheduling, cancellation, and completion requests shall provide the expected UpdatedAt from their appointment read; the system shall reject stale values and persist a strictly newer value after a successful change.

---

## FR-APT-007 — Appointment Status

The system shall maintain a status for each appointment.

Version 1 shall support:

- `Scheduled`
- `Cancelled`
- `Completed`

New appointments shall start as Scheduled. Only Scheduled → Cancelled and Scheduled → Completed transitions shall be allowed; Cancelled and Completed shall be terminal states.

---

## FR-APT-008 — Appointment Cancellation by Patient

A patient shall be able to cancel one of their future scheduled appointments.

---

## FR-APT-009 — Appointment Cancellation by Receptionist

A receptionist shall be able to cancel a future scheduled appointment on behalf of a patient.

---

## FR-APT-010 — Appointment Cancellation by Doctor

A doctor shall be able to cancel one of their future scheduled appointments when necessary.

---

## FR-APT-011 — Cancelled Appointment Slot

When an appointment is cancelled, its time slot shall become available again if it remains in the future, is covered by valid doctor availability, and satisfies scheduling rules.

Cancellation shall preserve the appointment record.

---

## FR-APT-012 — Appointment Rescheduling

An identity-verified patient with an Active User account shall be able to reschedule a future Scheduled appointment to another available time slot.

---

## FR-APT-013 — Receptionist Rescheduling

A receptionist shall be able to reschedule a future Scheduled appointment on behalf of an identity-verified patient with an Active User account.

---

## FR-APT-014 — Rescheduling Validation

A rescheduled appointment shall satisfy all booking rules, including patient verification and account status, doctor-specialty assignment, timing, availability, conflicts, and concurrency protection.

---

## FR-APT-015 — Atomic Rescheduling

The system shall prevent an appointment from being left in an invalid state if a rescheduling operation fails.

The existing appointment shall remain unchanged if the new appointment time cannot be successfully reserved.

Successful rescheduling shall update StartTime, EndTime, and UpdatedAt on the existing Appointment while preserving Id, PatientId, DoctorId, MedicalSpecialtyId, CreatedAt, and Scheduled status. It shall not cancel the appointment or create a replacement row.

The system shall exclude the appointment itself from its conflict check and coordinate simultaneous changes to the same appointment, according to [ADR-0010](adr/0010-rescheduled-existing-appointment.md).

---

## FR-APT-016 — Appointment Completion

A doctor shall be able to mark one of their scheduled appointments as completed.

---

## FR-APT-017 — Completion Restriction

A cancelled appointment shall not be allowed to transition to `Completed`.

---

## FR-APT-018 — Cancellation Restriction

A completed appointment shall not be allowed to transition back to `Scheduled` or `Cancelled`.

---

## FR-APT-019 — Patient Appointment Access

A patient shall only be able to access appointments associated with their own patient account.

---

## FR-APT-020 — Doctor Appointment Access

A doctor shall only be able to access appointment information associated with their own schedule unless another authorization rule explicitly permits access.

---

## FR-APT-021 — Upcoming Appointments

The system shall allow upcoming appointments to be retrieved in chronological order.

---

## FR-APT-022 — Appointment History

The system shall allow historical appointments to be retrieved separately from upcoming appointments.

---

## FR-APT-023 — Appointment Details

The system shall allow authorized users to retrieve the details of a specific appointment.

---

## FR-APT-024 — Appointment Duration and Time Policy

Booking and rescheduling shall require exactly 30-minute appointments starting on the hospital-local half-hour grid between 09:00 and 17:30 and ending no later than 18:00 on the same local date, Monday through Friday as evaluated in America/Montevideo.

The system shall interpret hospital dates and hours in America/Montevideo and exchange timestamp instants as ISO 8601 values with UTC or an explicit offset, according to [ADR-0009](adr/0009-appointment-time-policy.md).

---

## FR-APT-025 — Interruption Review and Cancellation Notice

Authorized receptionists shall be able to review future Scheduled appointments affected by doctor suspension/deactivation or specialty deactivation and cancel those that cannot proceed. Account or specialty changes shall not automatically cancel appointments.

This cancellation workflow shall require a patient-visible CancellationReason. Both patient clients shall display the Cancelled status, reason, and instructions to book another appointment or contact reception in appointment lists and details. Future cancelled appointments shall remain accessible to the patient.

The notice shall be based on persisted appointment data when loaded or refreshed, without requiring email, SMS, push, or real-time delivery. No Suspended appointment state shall be introduced. See [ADR-0011](adr/0011-service-interruption-cancellations.md).

---

# 12. Cross-Role Functional Rules

## FR-GEN-001 — Backend Enforcement

All security, authorization, account verification, and scheduling rules shall be enforced by the backend API.

Client applications shall not be considered authoritative for business-rule enforcement.

---

## FR-GEN-002 — Unauthorized Resource Access

The system shall reject requests attempting to access resources that the authenticated user is not authorized to access.

---

## FR-GEN-003 — Invalid Operations

The system shall reject operations that violate the current state of a resource.

Examples include:

- completing a cancelled appointment;
- booking an unavailable slot;
- assigning the same specialty to a doctor twice;
- booking an appointment from an unverified patient account.

---

## FR-GEN-004 — Consistent Clients

Business rules shall behave consistently regardless of whether a request originates from:

- the React web application;
- the React Native mobile application;
- an authorized API client.

---

# 13. Version 1 Functional Completion

The functional scope of Version 1 shall be considered complete when the system supports the following end-to-end workflows.

### Patient Registration and Verification

1. A patient creates an account.
2. The account enters `PendingVerification`.
3. The patient authenticates.
4. The patient can view that verification is required.
5. The patient visits the hospital.
6. A receptionist verifies the patient's identity.
7. Verification information is recorded.
8. The account becomes active.
9. The patient can access appointment booking functionality.

### Patient Appointment Booking

1. A verified patient authenticates.
2. The patient selects a medical specialty.
3. The patient selects a doctor.
4. The patient views available appointment slots.
5. The patient selects an available slot.
6. The system validates availability and booking rules.
7. The appointment is created.
8. The selected slot is no longer available to other users.

### Receptionist Appointment Booking

1. A receptionist authenticates.
2. The receptionist searches for a patient.
3. The system confirms that the patient is verified.
4. The receptionist selects a doctor.
5. The receptionist views available slots.
6. The receptionist creates an appointment for the patient.

### Doctor Workflow

1. A doctor authenticates.
2. The doctor manages their availability.
3. The doctor views upcoming appointments.
4. The doctor views the basic information of an associated patient.
5. The doctor completes or cancels an appointment when appropriate.

### Administration Workflow

1. An administrator authenticates.
2. The administrator creates doctor accounts and staff accounts for receptionists and administrators.
3. The administrator creates medical specialties.
4. The administrator assigns specialties to doctors.
5. The administrator manages account statuses when necessary.
