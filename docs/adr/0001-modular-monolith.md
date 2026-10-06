# ADR-0001 — Modular Monolith Organized by Feature

## Status

Accepted for Version 1.

## Context

Hospital Platform is developed as a portfolio project by one developer (me). Its initial workflows frequently coordinate accounts, profiles, availability, and appointments.

The project needs understandable module boundaries without the deployment and consistency complexity of distributed services.

## Decision

Build one ASP.NET Core backend organized into functional modules under Modules/. Each module contains Controllers, Services, DTOs, and Models where needed.

Start with one backend project and deploy the application as a single unit. Modules communicate through in-process service contracts and own mutations of their data.

Controllers handle HTTP; services coordinate use cases; models enforce local invariants; DTOs describe API inputs and outputs.

Add interfaces when they define a module boundary or separate an external dependency. Do not automatically introduce an interface for every class or a generic repository layer.

## Alternatives Considered

- Microservices: separate deployment and ownership, but require distributed coordination and additional operational work before there is a demonstrated need.
- One global Controllers/Services/Models structure: simple initially, but spreads each feature across unrelated folders and makes ownership less visible.
- Separate projects per module: stronger compile-time separation, but adds project and dependency management that is unnecessary for the initial scope.

## Consequences

- One build and deployment keep the initial development workflow manageable.
- Feature folders make responsibilities easier to locate.
- Folder boundaries do not prevent cross-module access; service contracts, reviews, and tests must preserve them.
- Module extraction would require further design and is not guaranteed merely by this organization.

## Revisit When

A module requires independent deployment, scaling, or ownership, or compile-time boundaries become necessary to control coupling.

## References

- [Architecture](../Architecture.md)
- [Scope](../Scope.md)
