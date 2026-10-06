# ADR-0002 — Separate Accounts from User Profiles

## Status

Accepted for Version 1. Exactly-one-profile persistence enforcement remains pending; employee-number registration is defined in ADR-0007.

## Context

Patients, doctors, and hospital staff share authentication information but have different domain data and permissions.

Patient identity verification must remain distinct from whether an account is currently allowed to access the system.

## Decision

Use User for shared names, email, password hash, AccountStatus, and account timestamps.

Associate each User with exactly one Patient, Doctor, or Staff profile in Version 1. Preserve separate profile Id and UserId attributes, with UserId unique within each profile table.

Represent Receptionist and Administrator through exactly one StaffRole on Staff. They are not separate profile entities.

Store patient verification through IdentityVerifiedAt and IdentityVerifiedByUserId. Both are absent before verification and populated together afterward. Account suspension or deactivation does not remove verification.

Keep EmployeeNumber on Doctor and Staff rather than introducing Employee. It must identify an employee uniquely across both profile types.

The technical EmployeeNumbers registry described in [ADR-0007](0007-employee-number-registry.md) centralizes number ownership without introducing an Employee domain entity.

## Alternatives Considered

- One User table containing all profile attributes: fewer tables, but mixes unrelated patient, medical, and staff information.
- Separate account tables for each actor: duplicates authentication and account-management responsibilities.
- Separate Receptionist and Administrator profiles: duplicates the shared staff structure without a Version 1 need.
- A shared Employee entity: could centralize employee information, but introduces an additional relationship for the current two employee types.
- Multiple profiles or staff roles per User: supports more combinations, but exceeds the agreed Version 1 model.

## Consequences

- Account operations are centralized while each module owns its profile data.
- Verified identity and account access remain independent.
- Account and profile creation must be atomic.
- Unique UserId constraints in each table do not enforce exactly one profile across all tables.
- Separate unique EmployeeNumber constraints do not enforce uniqueness across Doctor and Staff. ADR-0007 adds centralized number ownership and matching profile references; exactly-one-profile enforcement remains a separate pending decision.

## Revisit When

One person needs multiple profile types or staff roles, or additional employee categories justify shared Employee information.

## References

- [Domain Model](../DomainModel.md)
- [ERD](../ERD.md)
