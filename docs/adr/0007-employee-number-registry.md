# ADR-0007 — Central Employee Number Registry

## Status

Accepted for Version 1. Exactly-one-profile enforcement remains a separate pending decision.

## Context

Doctor and Staff each contain EmployeeNumber, which must identify an employee uniquely across both profile types. Independent unique constraints cannot prevent the same number from being assigned to different Users in different tables.

The agreed domain model excludes a shared Employee entity. Concurrent registrations must not rely on an application check followed by an insert into separate tables.

## Decision

Add a technical EmployeeNumbers table with:

- EmployeeNumber: text, required primary key;
- UserId: required unique foreign key to User.Id.

Each number has one account owner, and each account has at most one registered number.

Keep EmployeeNumber on Doctor and Staff. Add a UNIQUE (EmployeeNumber, UserId) key on the registry and a matching composite foreign key from each employee profile. Referencing only EmployeeNumber would not prove that the number belongs to the profile's User.

Create User, number registration, and Doctor or Staff together in one transaction. The Users module owns technical registry registration; the profile's module owns its profile data.

Keep registrations on account deactivation. Define normalization and number-generation policies before implementation; the selected format must be used consistently across the registry and profiles.

This table is not another User profile or an Employee domain entity. The separate exactly-one-profile rule is still required to prevent the same User from having both Doctor and Staff profiles.

## Alternatives Considered

- Unique constraints on Doctor and Staff alone: only enforce uniqueness within each table.
- Triggers checking both tables: preserve the existing table set, but require additional concurrency coordination and database logic for every relevant mutation.
- A shared Employee domain entity: centralizes more employment information than the current model requires.
- Distinct doctor and staff number prefixes: can separate number spaces, but impose a number-format policy that was not requested.

## Consequences

- One primary key centrally rejects duplicate number ownership, including concurrent assignments to different Users.
- Composite foreign keys reject profiles using another User's registered number.
- Registration introduces one additional table and transaction participant.
- EmployeeNumber is stored in the registry and profile; foreign keys keep the pair consistent.
- The registry does not ensure that every registration has a profile or enforce profile exclusivity. Atomic registration and the separate profile-consistency design must address those rules.
- Implementation tests must cover duplicate registration, mismatched ownership, rollback, and preservation after deactivation.

## Revisit When

Additional employee information justifies a shared Employee entity, or a User needs multiple employee profiles or registered numbers.

## References

- [User and Profile Model](0002-user-and-profile-model.md)
- [Domain Model](../DomainModel.md)
- [ERD](../ERD.md)
- [PostgreSQL unique and foreign-key constraints](https://www.postgresql.org/docs/current/ddl-constraints.html)
