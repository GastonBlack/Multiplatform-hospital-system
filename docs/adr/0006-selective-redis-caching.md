# ADR-0006 — Introduce Redis Only for Justified Caching

## Status

Accepted as an architectural policy. No initial cache workload or implementation is required by this record.

## Context

Redis is part of the planned learning path, but each technology must solve an actual problem. Appointment consistency and authorization cannot depend on temporarily stale cache data.

## Decision

Keep PostgreSQL authoritative. Introduce Redis when a measured read workload justifies caching and can tolerate temporary staleness.

Initial candidates are specialty listings and public doctor information. Do not use Redis as the authority for bookings, patient verification, or authorization decisions.

Before implementing a cache, define its keys, lifetime, invalidation behavior, and fallback to PostgreSQL when Redis is unavailable. Do not create caching abstractions before selecting a concrete use case.

## Alternatives Considered

- Cache every read immediately: adds invalidation work and can compromise decisions that require current information.
- Require Redis for booking correctness: makes critical writes depend on an additional coordination system.
- Avoid Redis permanently: simpler, but rules out a potentially useful optimization before workload measurements exist.

## Consequences

- The first functional implementation can operate without a cache.
- Cache introduction requires performance evidence and a documented tolerance for staleness.
- Selected cached reads need invalidation and fallback behavior.
- Multiple API instances can share cached information later, while critical validation continues against authoritative data.

## Revisit When

Measurements identify a specific caching benefit or the freshness requirements of a cached resource change.

## References

- [Architecture](../Architecture.md)
- [Scope — Performance](../Scope.md#83-performance)
- [Non-Functional Requirements](../NonFunctionalRequirements.md)
