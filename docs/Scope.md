# Hospital Platform — Project Scope

## 1. Project Overview

Hospital Platform is a multi-client healthcare management system designed to centralize the interaction between patients, doctors, receptionists, and hospital administrators.

The platform will provide a backend API built with ASP.NET Core, a mobile application focused primarily on patients, and a web application for patients and hospital staff.

The main objective of the first version is to provide a reliable and secure appointment management system while serving as the foundation for future hospital-related modules.

The project will also be used to implement and demonstrate production-oriented software engineering practices such as automated testing, caching, containerization, continuous integration and deployment, load balancing, observability, cloud deployment, and container orchestration.

---

## 2. Problem Statement

Hospitals and medical centers require a centralized system to manage doctors, medical specialties, patient accounts, doctor availability, and appointments.

Without a unified platform, appointment management can become fragmented across different systems or manual processes, increasing the likelihood of scheduling conflicts, duplicate bookings, inconsistent information, and administrative overhead.

Hospital Platform aims to provide a single system through which:

- patients can discover doctors and manage their appointments;
- doctors can manage their schedules and appointments;
- receptionists can assist patients, verify patient identities, and manage appointments;
- administrators can manage the hospital's users and medical structure.

---

## 3. Project Goals

The main goals of the project are:

- Build a complete backend API using modern ASP.NET Core and C#.
- Provide secure authentication and role-based authorization.
- Require in-person identity verification before patients can access appointment booking functionality.
- Allow patients to search for medical services and manage appointments after successful identity verification.
- Allow doctors to manage availability and their appointment schedules.
- Allow receptionists to verify patient identities and manage appointments.
- Allow administrators to manage doctors, specialties, users, and system-level configuration.
- Prevent conflicting or duplicate appointment bookings.
- Provide both web and mobile clients using the same backend API.
- Design the system with scalability and maintainability in mind.
- Introduce caching where it provides measurable value.
- Implement automated unit and integration testing.
- Containerize the application and its supporting services.
- Implement a complete CI/CD pipeline.
- Deploy the application to cloud infrastructure.
- Explore horizontal scaling and load balancing.
- Introduce Kubernetes as a later infrastructure and orchestration stage.
- Provide sufficient technical documentation to explain both implementation decisions and architectural trade-offs.

---

## 4. System Actors

Each actor authenticates through a `User` account associated with exactly one `Patient`, `Doctor`, or `Staff` profile in Version 1.

`User` contains shared personal information, credentials, `ProfileType`, and `AccountStatus`. ProfileType identifies Patient, Doctor, or Staff and must match the associated profile. Each profile references its account through `UserId`.

All account types use a unique canonical email: trim exterior whitespace and convert to lowercase, rejecting internal whitespace and preserving dots and plus suffixes. The same email-specific normalization applies to account creation, permitted email changes, and email-based login.

Database constraints and deferred validation must prevent committing accounts with missing or multiple profiles. Account and profile creation occur in one transaction.

Receptionist and Administrator are authorization roles represented by `StaffRole` on a `Staff` profile, not separate profile entities. Each Staff profile has exactly one StaffRole.

### 4.1 Patient

A patient is a user who consumes healthcare services through the platform.

Patients will be able to:

- register an account;
- authenticate into the system;
- manage their basic personal information;
- view their account verification status;
- browse medical specialties;
- search for doctors;
- view doctor information;
- view available appointment slots;
- book appointments after completing identity verification;
- cancel appointments;
- reschedule appointments after completing identity verification;
- view upcoming appointments;
- view past appointments.

Newly registered patient accounts will initially remain in a `PendingVerification` state.

Version 1 accepts only Uruguayan cédulas de identidad including the supplied check digit. Store their digits as unique text values, preserving leading zeros and the final digit; presentation spaces, dots, and hyphens are removed. For example, 1 234 324 1 is stored as 12343241. Version 1 does not calculate or validate the check-digit checksum. This format does not replace in-person identity verification.

Before being allowed to book or reschedule appointments, patients must visit the hospital in person and complete an identity verification process with an authorized receptionist.

After successful verification, the receptionist may activate an eligible `PendingVerification` account. Verification does not automatically reactivate a suspended or deactivated account.

---

### 4.2 Doctor

A doctor is a medical professional registered in the platform.

The Doctor profile contains `EmployeeNumber` and `MedicalLicenseNumber`.

`MedicalLicenseNumber` is required and unique across all Doctor profiles, including those whose User account is Suspended or Deactivated. Version 1 assumes a single medical-license numbering system; account status changes do not release the number for another profile.

Doctors will be able to:

- authenticate into the system;
- view their assigned medical specialties;
- define and manage their availability;
- view their appointment schedule;
- view basic information about patients associated with their appointments;
- cancel appointments when necessary;
- mark appointments as completed.

Doctor accounts will not be created through public registration.

They will be created and managed by administrators.

---

### 4.3 Receptionist

A receptionist is a hospital employee who assists patients with administrative operations.

The receptionist has a Staff profile with `StaffRole.Receptionist` and an `EmployeeNumber`.

Receptionists will be able to:

- authenticate into the system;
- search for patients;
- register patients when necessary;
- verify patient identities in person;
- activate patient accounts after successful identity verification;
- view patient verification status;
- view doctor schedules;
- view available appointment slots;
- create appointments on behalf of verified patients;
- cancel appointments;
- reschedule appointments;
- view appointment information required for administrative purposes.

When a receptionist registers a patient during an in-person visit and successfully verifies the patient's identity during the same process, the patient account may be activated immediately.

Receptionists will not have access to system administration functionality.

---

### 4.4 Administrator

An administrator is responsible for managing the platform's hospital-level configuration.

The administrator has a Staff profile with `StaffRole.Administrator` and an `EmployeeNumber`.

Administrators will be able to:

- authenticate into the system;
- create and manage doctor accounts;
- create and manage staff accounts for receptionists and administrators;
- manage user account status;
- create and manage medical specialties;
- assign specialties to doctors;
- manage doctor information;
- review relevant system-level information.

Administrators will not automatically have access to private medical information outside the administrative requirements of the platform.

---

## 5. In Scope

Version 1 will include the following functionality.

### 5.1 Authentication and Authorization

- User authentication.
- Secure password storage.
- JWT-based authentication.
- Access tokens.
- Refresh tokens.
- Refresh token rotation and revocation.
- Role-based authorization.
- Account status management.
- Account activation and deactivation.
- Protected API endpoints.
- Authentication support for web and mobile clients.

Each login creates an independent session, allowing web and mobile sessions for the same User. Access JWTs last at most 15 minutes; sessions expire absolutely 7 days after login. Refresh tokens rotate without extending session expiry.

Protected requests check current session and account/profile authorization against PostgreSQL. Logout revokes the current session; suspension or deactivation revokes the User's sessions and reactivation requires a new login. See [ADR-0012](adr/0012-authentication-sessions.md).

User accounts may have states such as:

- `PendingVerification`
- `Active`
- `Suspended`
- `Deactivated`

Patient account verification and general account status are related but conceptually separate concerns.

`PendingVerification` patients may log in, refresh their session, log out, and read their own account/profile, account and identity-verification status, and verification instructions. Other protected patient operations, including profile updates and appointment management, are unavailable until activation. Public specialty and doctor information remains accessible. Booking and rescheduling require verified identity and an Active account.

A patient must have an active and identity-verified account before appointment booking or rescheduling functionality is enabled.

---

### 5.2 Patient Management

- Patient self-registration.
- Patient registration by authorized reception staff.
- Patient profile management.
- Patient search for authorized staff.
- Patient account status management.
- In-person identity verification.
- Recording when a patient identity was verified.
- Recording which authorized user performed the verification.
- Access to appointment history.
- Access to upcoming appointments.

Self-registered patient accounts will initially be assigned a `PendingVerification` status.

Patients must visit the hospital and present valid identification to an authorized receptionist.

After successful verification, the receptionist may activate an eligible `PendingVerification` account.

Relevant verification information should be auditable, including:

- `Patient.IdentityVerifiedAt`;
- `Patient.IdentityVerifiedByUserId`.

Verification status is determined from these attributes rather than a separate persisted boolean.

---

### 5.3 Doctor Management

- Doctor account creation by administrators.
- Doctor profile management.
- Doctor activation and deactivation.
- Medical specialty assignment.
- Specialty assignment removal only when the doctor has no future Scheduled appointments for that specialty; reception must cancel affected appointments first with a patient-visible reason and the existing in-app guidance. Historical appointments are preserved.
- Doctor search and filtering.
- Doctor schedule access.

`EmployeeNumber` must be unique across Doctor and Staff profiles. A technical `EmployeeNumbers` registry associates each number with one User account. Version 1 does not introduce a shared Employee domain entity.

The system assigns immutable numbers automatically from a shared sequence in the format EMP-000001, with at least six digits. Gaps are allowed and numbers are not reused after suspension/deactivation. Employee numbers are internal and visible only to authorized hospital personnel; User.Id remains UUID.

---

### 5.4 Medical Specialty Management

- Creation of medical specialties.
- Modification of medical specialties.
- Activation or deactivation of specialties.
- Assignment of one or more specialties to doctors.
- Search for doctors by specialty.

---

### 5.5 Doctor Availability

Doctors will be able to define the periods during which they are available for appointments.

The system will:

- store doctor availability;
- validate availability ranges;
- prevent invalid or overlapping availability definitions;
- calculate available appointment slots;
- exclude already booked times;
- prevent bookings outside a doctor's availability.

Availability periods are persisted; bookable slots are calculated dynamically and are not persisted as separate entities in Version 1.

Version 1 uses 30-minute appointments Monday through Friday within 09:00–18:00 in America/Montevideo, starting on the half-hour grid from 09:00 through 17:30. Saturday and Sunday are not bookable. A doctor available for the entire window has at most 18 potential daily slots before existing bookings are excluded.

There is no holiday calendar or automatic holiday exclusion in Version 1. Doctors omit availability on concrete dates when they will not attend. If appointments already exist for a later absence, reception cancels those affected with a patient-visible reason and in-app guidance before availability is removed or changed.

---

### 5.6 Appointment Management

The system will support:

- appointment creation;
- appointment cancellation;
- appointment rescheduling;
- appointment completion;
- appointment history;
- upcoming appointment retrieval;
- doctor schedule retrieval;
- patient schedule retrieval.

Each appointment references a Patient, a Doctor, and a MedicalSpecialty directly. Booking and rescheduling must validate that the doctor is assigned to the selected specialty.

Rescheduling changes the existing appointment's time atomically, preserving its Id, patient, doctor, specialty, and Scheduled status. A failed operation leaves the original appointment unchanged. Version 1 does not introduce replacement records or a separate history of previous intervals.

Doctor suspension/deactivation and specialty deactivation block new bookings and rescheduling for the affected selection but do not automatically cancel existing appointments or remove availability. Reception reviews future Scheduled appointments and cancels those affected through the normal workflow, recording a patient-visible CancellationReason.

Both patient clients display cancellation status, reason, and instructions to book another appointment or contact reception in appointment lists and details, including future cancelled appointments. Notices appear when persisted data is loaded or refreshed. Email, SMS, push, and real-time notification delivery are not required in Version 1. No Suspended appointment state is introduced.

The system must prevent:

- unverified patients from booking appointments;
- unverified patients from rescheduling appointments;
- booking appointments in the past;
- booking outside doctor availability;
- conflicting appointments for the same doctor;
- duplicate bookings of the same time slot;
- invalid appointment state transitions.

Appointment booking must be designed to remain correct when multiple users attempt to reserve the same slot concurrently.

Patient identity verification must be enforced by the backend and must not rely only on client-side restrictions.

---

### 5.7 Web Application

A React-based web application will consume the Hospital Platform API.

The web application will provide interfaces according to the authenticated user's role.

Possible areas include:

- Patient Portal;
- Doctor Portal;
- Receptionist Portal;
- Administration Portal.

The Patient Portal should clearly display whether an account is still awaiting in-person identity verification.

The web client will not contain independent business logic that bypasses backend validation.

The backend API will remain the authoritative source for business rules.

---

### 5.8 Mobile Application

A React Native mobile application will consume the same Hospital Platform API.

Version 1 of the mobile application will primarily target patients.

Core functionality will include:

- authentication;
- viewing account verification status;
- viewing medical specialties;
- searching for doctors;
- viewing doctor availability;
- booking appointments after identity verification;
- cancelling appointments;
- rescheduling appointments after identity verification;
- viewing upcoming appointments;
- viewing appointment history.

Patients whose accounts are awaiting verification should receive clear information explaining that they must visit the hospital reception desk before appointment booking becomes available.

Administrative functionality is not required in the mobile application for Version 1.

---

## 6. Out of Scope

The following functionality will not be implemented in Version 1.

### 6.1 Payments and Billing

The system will not process payments.

This includes:

- credit or debit card payments;
- payment gateways;
- invoices;
- insurance billing;
- refunds;
- hospital billing workflows.

The architecture should allow payment functionality to be integrated in the future as an independent billing or payment service.

---

### 6.2 Electronic Medical Records

Version 1 will not implement a complete Electronic Medical Record or Electronic Health Record system.

The following are excluded:

- medical diagnoses;
- detailed medical histories;
- clinical notes;
- treatment plans;
- medical documents;
- test results.

Only the minimum patient information required for identity verification and appointment management will be stored.

---

### 6.3 Prescriptions

The platform will not manage:

- medication prescriptions;
- prescription history;
- pharmacy integration;
- medication inventory.

---

### 6.4 Laboratory Management

The platform will not manage:

- laboratory orders;
- laboratory samples;
- laboratory results;
- laboratory workflows.

---

### 6.5 Pharmacy Management

The platform will not include:

- medication stock management;
- pharmacy sales;
- medication distribution;
- supplier management.

---

### 6.6 Hospital Inventory

Hospital equipment and general inventory management are outside the scope of Version 1.

---

### 6.7 Insurance Processing

The platform will not implement:

- insurance company integrations;
- insurance authorization;
- insurance coverage validation;
- claim processing.

---

### 6.8 Emergency Department Management

Emergency room workflows, triage systems, emergency admissions, and emergency prioritization are outside the scope of Version 1.

---

## 7. Main Functional Areas

The system will be logically divided into the following functional areas:

- Authentication
- Users
- Patients
- Patient Verification
- Doctors
- Medical Specialties
- Doctor Availability
- Appointments
- Administration

The backend should maintain clear boundaries between these areas to allow the system to evolve without unnecessary coupling.

---

## 8. Non-Functional Goals

### 8.1 Security

The system should:

- require authentication for protected operations;
- enforce authorization according to user roles and policies;
- prevent unverified patients from accessing restricted appointment operations;
- securely hash passwords;
- protect authentication tokens;
- validate all external input;
- avoid exposing sensitive internal information through API errors;
- use HTTPS in production;
- securely manage application secrets;
- apply reasonable rate limiting where necessary.

Identity verification operations should only be available to authorized staff with `StaffRole.Receptionist`.

Verification actions should be auditable.

---

### 8.2 Reliability

The platform should maintain consistent appointment information even when multiple users interact with the same scheduling resources simultaneously.

Critical operations such as appointment booking must use appropriate database constraints, transactions, and concurrency controls.

Patient verification and account status must be persisted reliably and enforced consistently across all clients.

---

### 8.3 Performance

The API should provide acceptable response times under expected workloads.

Redis may be used to cache information that:

- is frequently requested;
- changes relatively infrequently;
- can safely tolerate temporary cache staleness.

Caching must not compromise appointment consistency, patient verification status, or authorization decisions.

---

### 8.4 Scalability

The backend should be designed as a stateless API whenever possible.

This will allow multiple API instances to run simultaneously behind a load balancer.

Shared application state should be stored in appropriate external services such as PostgreSQL or Redis instead of individual API instances.

---

### 8.5 Availability

The deployment architecture should eventually support:

- multiple API instances;
- load balancing;
- health checks;
- automatic replacement of failed application instances;
- container orchestration.

---

### 8.6 Maintainability

The codebase should:

- separate business concerns clearly;
- avoid unnecessary coupling;
- follow consistent coding conventions;
- include automated tests;
- document relevant architectural decisions;
- remain understandable to developers unfamiliar with the project.

---

### 8.7 Observability

The deployed system should provide enough information to understand its runtime behavior.

This includes:

- structured logging;
- centralized application logs;
- health checks;
- request tracing or correlation identifiers;
- relevant application and infrastructure metrics.

Security-sensitive operations such as patient identity verification should produce appropriate audit information.

Additional observability tooling may be introduced as the project evolves.

---

## 9. Technical Scope

### Backend

- ASP.NET Core
- .NET 10
- C#
- Entity Framework Core
- PostgreSQL
- Redis
- JWT authentication
- REST API
- OpenAPI documentation

### Web

- React
- TypeScript

### Mobile

- React Native
- TypeScript

### Testing

- Unit tests
- Integration tests
- End-to-end testing where appropriate
- Testcontainers for integration environments where useful
- Load testing during the infrastructure stage

### Infrastructure

The infrastructure learning path may include:

- Docker
- Docker Compose
- Reverse proxy concepts
- Load balancing
- AWS
- Container registries
- Multiple backend instances
- Kubernetes
- Managed Kubernetes through Amazon EKS or equivalent infrastructure

Kubernetes is intentionally considered a later project stage and is not required for the initial application implementation.

### CI/CD

The project should eventually include an automated pipeline capable of performing operations such as:

- dependency restoration;
- application build;
- automated tests;
- container image creation;
- container registry publishing;
- automated deployment;
- deployment health verification.

---

## 10. Architectural Direction

The initial backend will be developed as a modular monolith.

The project will not begin as a microservices architecture.

The application should maintain clear boundaries between functional areas so that individual modules could potentially be extracted into separate services in the future if real scalability or organizational requirements justify doing so.

Infrastructure complexity should only be introduced when it solves a specific problem or provides a concrete learning objective.

---

## 11. Assumptions

The initial project assumes that:

- a hospital organization manages the platform;
- administrators are trusted internal hospital users;
- doctors are registered by administrators;
- patients may create their own accounts;
- self-registered patient accounts initially remain in a `PendingVerification` state;
- patients must complete an in-person identity verification process with an authorized receptionist before appointment booking and rescheduling are enabled;
- receptionists are responsible for verifying patient identities and activating eligible patient accounts;
- when a patient is registered in person by a receptionist and their identity is verified during registration, the account may be activated immediately;
- identity verification actions should record when verification occurred and which authorized user performed it;
- doctor availability is configured within the platform;
- appointments last exactly 30 minutes and fit within the Monday–Friday 09:00–18:00 America/Montevideo operating window and doctor availability;
- all clients communicate through the backend API;
- PostgreSQL is the authoritative persistent data store;
- Redis is not the authoritative source for critical business information;
- payment processing is handled externally or by a future system.

---

## 12. Future Improvements

Potential future versions may include:

- payment and billing services;
- insurance integration;
- electronic medical records;
- prescriptions;
- laboratory integration;
- pharmacy management;
- notifications through email, SMS, or push notifications;
- video consultations;
- audit dashboards;
- advanced reporting;
- multi-hospital support;
- advanced scheduling rules;
- event-driven integrations;
- extraction of selected modules into independent services when justified.

These features are considered possible extensions and are not requirements for Version 1.

---

## 13. Version 1 Success Criteria

Version 1 will be considered functionally complete when:

- all required user roles can authenticate and perform their defined operations;
- new patient accounts correctly enter a pending verification state when appropriate;
- receptionists can verify patient identities and activate eligible accounts;
- patient verification actions are auditable;
- unverified patients cannot book or reschedule appointments;
- patients can search for doctors, and verified patients with active accounts can book and reschedule appointments;
- doctors can manage availability and appointments;
- receptionists can manage appointments on behalf of verified patients;
- administrators can manage user accounts, doctor and staff profiles, and specialties;
- conflicting appointment bookings are correctly prevented;
- the API is covered by meaningful automated tests;
- the web application consumes the production API;
- the mobile application consumes the production API;
- the system can be deployed through an automated CI/CD process;
- multiple API instances can operate safely;
- the infrastructure demonstrates load balancing and container orchestration concepts;
- the project contains sufficient technical documentation to explain its design and architectural decisions.
