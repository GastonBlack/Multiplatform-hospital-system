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

---

## FR-AUTH-002 — Invalid Credentials

The system shall reject authentication attempts when the supplied credentials are invalid.

---

## FR-AUTH-003 — Access Token

The system shall issue an access token after successful authentication.

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

---

## FR-AUTH-007 — Logout

The system shall allow authenticated users to terminate their active session by revoking the associated refresh token.

---

## FR-AUTH-008 — Role Assignment

Every user account shall have an assigned system role.

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

The system shall maintain an account status for every user.

Supported account statuses shall include at least:

- `PendingVerification`
- `Active`
- `Suspended`
- `Deactivated`

---

## FR-AUTH-011 — Suspended Accounts

The system shall prevent suspended users from performing protected operations.

---

## FR-AUTH-012 — Deactivated Accounts

The system shall prevent deactivated users from performing protected operations.

---

## FR-AUTH-013 — Current User Information

The system shall allow an authenticated user to retrieve their own basic account and profile information.

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

---

## FR-PAT-004 — Unique Email

The system shall prevent multiple user accounts from being registered with the same email address.

---

## FR-PAT-005 — Unique National Identification

The system shall prevent multiple patient records from using the same national identification number.

---

## FR-PAT-006 — Patient Profile

An authenticated patient shall be able to view their own profile.

---

## FR-PAT-007 — Patient Profile Update

A patient shall be able to modify profile information that the system allows patients to manage directly.

Information considered sensitive or related to verified identity may require hospital staff intervention.

---

## FR-PAT-008 — Verification Status

A patient shall be able to view whether their account is awaiting identity verification or has already been verified.

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

- that the identity was verified;
- the date and time of verification;
- the authorized user who performed the verification.

---

## FR-VER-005 — Account Activation

After successful identity verification, the system shall allow the patient's account to become `Active`.

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

The system shall maintain profile information for every doctor.

---

## FR-DOC-003 — Doctor Status

The system shall allow administrators to activate, suspend, or deactivate doctor accounts.

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

## FR-ADM-002 — Receptionist Account Management

An administrator shall be able to create and manage receptionist accounts.

---

## FR-ADM-003 — User Status Management

An administrator shall be able to change the status of eligible user accounts.

---

## FR-ADM-004 — Medical Specialty Management

An administrator shall be able to create and manage medical specialties.

---

## FR-ADM-005 — Specialty Assignment

An administrator shall be able to assign medical specialties to doctors.

---

## FR-ADM-006 — Specialty Removal

An administrator shall be able to remove a medical specialty assignment from a doctor.

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
- existing appointments;
- applicable scheduling rules.

---

## FR-AVL-008 — Occupied Slots

A time slot containing an active appointment shall not be returned as available.

---

## FR-AVL-009 — Availability Retrieval

Patients and authorized hospital staff shall be able to retrieve available appointment slots for a doctor.

---

# 11. Appointment Management

## FR-APT-001 — Appointment Creation

The system shall allow a verified patient to book an available appointment.

---

## FR-APT-002 — Appointment Creation by Receptionist

The system shall allow a receptionist to create an appointment on behalf of a verified patient.

---

## FR-APT-003 — Doctor Availability Requirement

An appointment shall only be created within a valid availability period for the selected doctor.

---

## FR-APT-004 — Past Appointments

The system shall reject attempts to create appointments in the past.

---

## FR-APT-005 — Doctor Scheduling Conflict

The system shall prevent a doctor from having multiple active appointments that overlap in time.

---

## FR-APT-006 — Concurrent Booking

When multiple users attempt to book the same appointment slot concurrently, the system shall allow at most one booking to succeed.

---

## FR-APT-007 — Appointment Status

The system shall maintain a status for each appointment.

Version 1 shall support at least:

- `Scheduled`
- `Cancelled`
- `Completed`

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

When an appointment is cancelled, its time slot shall become available again when the doctor's availability still permits that slot.

---

## FR-APT-012 — Appointment Rescheduling

A verified patient shall be able to reschedule a future scheduled appointment to another available time slot.

---

## FR-APT-013 — Receptionist Rescheduling

A receptionist shall be able to reschedule a future scheduled appointment on behalf of a verified patient.

---

## FR-APT-014 — Rescheduling Validation

A rescheduled appointment shall be subject to the same availability and conflict rules as a newly created appointment.

---

## FR-APT-015 — Atomic Rescheduling

The system shall prevent an appointment from being left in an invalid state if a rescheduling operation fails.

The existing appointment shall remain valid unless the new appointment time is successfully reserved.

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
2. The administrator creates doctor and receptionist accounts.
3. The administrator creates medical specialties.
4. The administrator assigns specialties to doctors.
5. The administrator manages account statuses when necessary.