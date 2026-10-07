# ADR-0007 — Central Employee Number Registry

## Status

Accepted for Version 1. Exactly-one-profile enforcement is defined separately in ADR-0008.

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

Keep registrations on account suspension or deactivation. Numbers are assigned automatically and are not editable or reused for another employee.

### Number Generation and Format

The Users registration operation obtains a value from one PostgreSQL bigint sequence shared by Doctor and Staff, starting at 1, incrementing by 1, with NO CYCLE. Format it as the uppercase literal EMP- followed by the decimal value padded to at least six digits: EMP-000001, EMP-000002, and so on. Values beyond six digits retain every digit; formatting must never truncate the sequence value.

Persist the same canonical text in EmployeeNumbers and the employee profile within the registration transaction. Clients do not supply or edit the number. Do not allocate through MAX + 1, a count, or process-local counters. Do not reset the sequence or recycle unused values during normal operation.

Rollback removes the uncommitted account, registry row, and profile, but does not reclaim an allocated sequence value. Gaps are acceptable; numbers do not represent a reliable employee count or commit order. Return the assigned number only after registration commits.

EmployeeNumber is an internal identifier exposed only to authorized hospital personnel, not in public doctor data or patient responses. It is separate from User.Id, which remains UUID, and is not an authentication credential.

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
- The registry does not ensure that every registration has an employee profile or enforce profile exclusivity. Atomic employee registration and [ADR-0008](0008-single-user-profile.md) supply the profile-consistency design; registry registration must be restricted to Doctor and Staff accounts.
- Implementation tests must cover concurrent Doctor/Staff allocation, canonical formatting including values beyond six digits, duplicate registration, mismatched ownership, rollback with acceptable sequence gaps, and preservation after suspension/deactivation.

## Revisit When

Additional employee information justifies a shared Employee entity, or a User needs multiple employee profiles or registered numbers.

## References

- [User and Profile Model](0002-user-and-profile-model.md)
- [Domain Model](../DomainModel.md)
- [ERD](../ERD.md)
- [PostgreSQL unique and foreign-key constraints](https://www.postgresql.org/docs/current/ddl-constraints.html)
- [PostgreSQL sequence functions and rollback behavior](https://www.postgresql.org/docs/current/functions-sequence.html)
