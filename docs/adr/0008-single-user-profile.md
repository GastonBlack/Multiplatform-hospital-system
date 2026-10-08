# ADR-0008 — Enforce Exactly One Profile per User

## Status

Accepted for Version 1. Implemented in InitialAccounts and validated against a disposable PostgreSQL database, including commit-time rejection, rollback, and conflicting concurrent writes. Automated regression tests in the repository and CI remain pending.

## Context

Every User must have exactly one Patient, Doctor, or Staff profile in Version 1.

A unique UserId in each profile table prevents duplicate profiles of the same type. It does not prevent a User from appearing in different profile tables or from having no profile.

EmployeeNumbers resolves employee-number ownership, not profile exclusivity. The application must create an account and its profile atomically, but the database also needs to reject invalid committed states.

## Decision

Use a profile-type discriminator, composite foreign keys, and a deferred existence check.

### At Most One Profile

- Add a required User.ProfileType restricted to Patient, Doctor, or Staff.
- Declare UNIQUE (Id, ProfileType) on User as the composite foreign-key target.
- Give each profile table a required ProfileType column fixed to its table's value through a CHECK constraint: Patient, Doctor, or Staff.
- Retain the required unique UserId in each profile table.
- Reference User through the composite pair (UserId, ProfileType).

For example, a Patient profile can reference only a User whose ProfileType is Patient. A Doctor profile cannot reference that same User because its fixed Doctor value would not match.

The unique UserId constraint then limits the matching profile table to one row for that User.

These fields are persistence discriminators. ProfileType distinguishes Patient, Doctor, and Staff; StaffRole still distinguishes Receptionist and Administrator. The two concepts must not be merged.

### At Least One Profile

Add deferred constraint triggers that validate the final profile state for each affected User before its transaction commits.

Queue checks when a User is inserted or its profile-related key changes, and when a profile is inserted, deleted, or its account linkage changes. For linkage changes, validate both old and new account owners.

The check requires exactly one matching profile for each User that still exists at validation time. A transaction attempting to create an account without its profile or remove its only profile must fail.

The check must run after account and profile creation can both occur. An immediate existence check on User insertion would reject the necessary intermediate state before the profile is inserted.

Normal account deactivation changes AccountStatus and preserves the profile. This design does not introduce a profile-conversion workflow.

### Transaction and Mutation Rules

Create User and its profile in one transaction. For employees, include EmployeeNumbers registration in the same transaction.

Use immediate composite foreign keys for type matching and initially deferred constraint triggers for profile existence. Services must handle validation failures occurring at commit, not only at SaveChanges.

Under READ COMMITTED, immediate profile-mutation triggers lock affected User rows with FOR UPDATE in UUID order, including old and new owners for linkage changes. User mutations already lock their own row. Deferred checks acquire the owner lock and query the final profile state in a subsequent statement. Services affecting several accounts must also acquire their User locks in UUID order before mutations. Do not grant the application a way to disable constraints; InitialAccounts rejects TRUNCATE on account, profile, and registry tables.

## Alternatives Considered

- Application validation alone: the simplest implementation, but database writes outside that workflow can commit invalid account/profile combinations.
- Discriminator and composite foreign keys without deferred checks: enforce at most one profile, but permit an account with no profile.
- Triggers counting profiles without a discriminator: avoid additional columns, but move exclusivity and its concurrent-write coordination into procedural logic.
- Profile references stored on User: can express selection through additional keys, but introduce reverse references and insertion dependencies while profile UserId still needs ownership validation.
- One combined profile table: makes exclusive storage easier but changes the accepted separate-profile model.

## Consequences

- Most exclusivity enforcement uses declarative keys and constraints.
- Deferred validation permits atomic creation while rejecting a missing profile at commit.
- The design adds discriminator columns and database trigger logic, including EF Core migration work.
- A shared DbContext or an application type check is not sufficient on its own.
- SQL-level tests must cover direct inserts without profiles, mismatched profile types, duplicate profiles, profile deletion, transaction rollback, and simultaneous conflicting writes.
- The ERD, domain attributes, architecture, requirements, and related ADRs describe this selected design. Documentation does not substitute for migration and concurrency validation.

## Revisit When

A User needs multiple profile types or the project changes the accepted profile storage model.

## References

- [User and Profile Model](0002-user-and-profile-model.md)
- [Employee Number Registry](0007-employee-number-registry.md)
- [ERD](../ERD.md)
- [PostgreSQL foreign-key constraints](https://www.postgresql.org/docs/current/ddl-constraints.html#DDL-CONSTRAINTS-FK)
- [PostgreSQL deferred constraint triggers](https://www.postgresql.org/docs/current/sql-createtrigger.html)
