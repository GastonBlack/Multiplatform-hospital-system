# Hospital Platform — Non-Functional Requirements

## 1. Purpose

This document defines the non-functional requirements for Version 1 of the Hospital Platform.

While functional requirements describe what the system must do, non-functional requirements define the quality attributes and operational characteristics the system should provide.

These requirements will guide decisions related to:

- security;
- performance;
- scalability;
- reliability;
- availability;
- maintainability;
- observability;
- testing;
- deployment;
- infrastructure.

Each requirement is assigned a unique identifier so that architectural decisions and future tests can be traced back to a specific system requirement.

---

## 2. Requirement Conventions

Non-functional requirement identifiers follow the format:

`NFR-{CATEGORY}-{NUMBER}`

Categories used in this document:

- `SEC` — Security
- `PERF` — Performance
- `SCA` — Scalability
- `REL` — Reliability and Resilience
- `DATA` — Data Integrity and Persistence
- `CACHE` — Caching
- `OBS` — Observability
- `MAINT` — Maintainability
- `TEST` — Testing
- `DEP` — Deployment
- `CICD` — Continuous Integration and Deployment
- `OPS` — Operational Requirements

---

# 3. Security

## NFR-SEC-001 — HTTPS

All production communication between clients and the backend API shall use HTTPS.

Plain HTTP shall not be used for authenticated production traffic.

---

## NFR-SEC-002 — Password Storage

User passwords shall never be stored in plaintext.

Passwords shall be processed using a secure password hashing mechanism suitable for authentication.

---

## NFR-SEC-003 — Authentication Tokens

Authentication tokens shall have limited validity periods.

Access tokens shall be short-lived compared to refresh tokens.

Access JWTs shall last at most 15 minutes, capped by the session expiry. Each independent login session shall expire absolutely 7 days after creation; refresh rotation shall not extend this deadline. JWT validation shall verify trusted signatures, allowed algorithms, issuer, audience, and expiry, as defined in [ADR-0012](adr/0012-authentication-sessions.md).

---

## NFR-SEC-004 — Refresh Token Storage

Refresh tokens shall be stored and managed in a way that allows:

- expiration;
- revocation;
- rotation;
- detection of invalid or previously revoked tokens.

Sensitive token values shall not be unnecessarily exposed or logged.

Refresh tokens shall be generated from 32 cryptographically random bytes. PostgreSQL shall persist unique SHA-256 token hashes rather than raw credentials, retaining consumed versions until session expiry for reuse detection. Rotation and reuse revocation shall be transactional and coordinated by locking the session row.

---

## NFR-SEC-005 — Authorization

Protected resources shall enforce authorization on the backend.

Authorization shall be consistent with the User's single Patient, Doctor, or Staff profile and, for Staff, its StaffRole. AccountStatus shall be evaluated independently of patient identity verification.

PendingVerification accounts shall receive only permitted access, including account information and verification instructions. Suspended and Deactivated accounts shall not perform protected operations.

Client-side UI restrictions shall not be considered a security boundary.

Protected requests shall verify current session ownership/state and account/profile authorization in PostgreSQL, independently of stale JWT role information. Logout revokes the current session; account suspension/deactivation revokes all of the User's sessions atomically with the status change. This policy requires shared database authorization reads across API instances.

---

## NFR-SEC-006 — Input Validation

All externally supplied input shall be validated before being processed by domain or persistence logic.

Invalid input shall be rejected using consistent API responses.

---

## NFR-SEC-007 — Secrets Management

Production secrets shall not be committed to source control.

This includes:

- database credentials;
- JWT signing secrets;
- Redis credentials;
- cloud credentials;
- third-party service credentials.

Secrets shall be provided through secure configuration or secret-management mechanisms.

---

## NFR-SEC-008 — Sensitive Logging

Logs shall not intentionally contain:

- plaintext passwords;
- authentication tokens;
- refresh tokens;
- private cryptographic keys.

Personally identifiable information should only be logged when operationally necessary.

---

## NFR-SEC-009 — Rate Limiting

The API should support rate limiting for endpoints that are sensitive to abuse.

Examples include:

- authentication;
- registration;
- token refresh;
- other endpoints identified as abuse-sensitive.

---

## NFR-SEC-010 — Error Exposure

Production API responses shall not expose internal implementation details such as:

- stack traces;
- database connection information;
- internal exception details;
- infrastructure credentials.

---

## NFR-SEC-011 — Identity Verification Authorization

Patient identity verification operations shall only be available to authorized Users with a Staff profile and StaffRole.Receptionist.

Verification operations shall reliably persist Patient.IdentityVerifiedAt and Patient.IdentityVerifiedByUserId together to determine who performed the action and when. Account suspension or deactivation shall not remove this verification record.

---

# 4. Performance

## NFR-PERF-001 — API Response Time

Common API read operations should provide responsive performance under the documented reference workload.

Examples include:

- medical specialty listing;
- doctor searches;
- doctor details;
- upcoming appointment retrieval.

An initial performance target should aim for a 95th percentile server response time below approximately 500 milliseconds for common cached or database-backed read operations under normal expected load.

---

## NFR-PERF-002 — Critical Write Operations

Critical write operations such as appointment booking should prioritize consistency and correctness over minimum latency.

Under normal conditions, these operations should still complete without unnecessary delays.

---

## NFR-PERF-003 — Database Query Efficiency

Database access shall avoid unnecessary repeated queries and excessive data retrieval.

Queries should:

- retrieve only required data where practical;
- use appropriate indexes;
- support pagination for potentially large result sets;
- avoid known N+1 query patterns.

---

## NFR-PERF-004 — Pagination

Endpoints returning collections that may grow significantly shall support pagination.

Examples include:

- patients;
- doctors;
- appointments;
- administrative user searches.

---

## NFR-PERF-005 — Load Testing

The deployed backend shall eventually be evaluated using load testing.

Load tests should measure at least:

- requests per second;
- response latency;
- error rate;
- CPU usage;
- memory usage;
- database behavior.

The exact reference workload shall be documented when performance testing is implemented.

---

# 5. Scalability

## NFR-SCA-001 — Stateless API

The backend API shall be designed to remain stateless between HTTP requests whenever possible.

Application correctness shall not depend on a request reaching the same API instance that handled a previous request.

---

## NFR-SCA-002 — Horizontal Scaling

The backend shall support horizontal scaling through multiple API instances.

For example:

```text
Load Balancer
    |
    +-- API Instance 1
    |
    +-- API Instance 2
    |
    +-- API Instance N
```
