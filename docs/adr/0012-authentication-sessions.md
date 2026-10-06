# ADR-0012 — JWT Access Tokens and Rotating Refresh Sessions

## Status

Accepted for Version 1: session lifecycle, token lifetimes, rotation, and current database authorization checks. Browser token storage remains a proposal pending deployment-origin and CSRF design; signing-key and issuer details require implementation design.

## Context

React Web and React Native access the same protected API. Authentication must continue working across multiple API instances and react to logout, account suspension, and permission changes.

A signed access token can remain cryptographically valid after its owner's permissions or session state change. Refresh-token rotation also needs to remain correct under concurrent requests.

## Decision

### Access Tokens

- Use signed JWT access tokens with a maximum lifetime of 15 minutes.
- Include sub = User.Id, sid = authentication session Id, jti, iss, aud, iat, and exp.
- Validate the signature with trusted keys, allowed signing algorithms, issuer, audience, and expiration before trusting claims.
- Treat claims as readable data, not encrypted secrets; do not include passwords, identity documents, or verification details.
- Derive current permissions from User.ProfileType and Staff.StaffRole in authoritative data instead of treating a token's role snapshot as current authorization.

The JWT identifies the caller. Own-account operations obtain User.Id from the validated identity; resource operations must separately validate ownership and permissions.

### Sessions and Refresh Tokens

- Create one persistent session per successful login; allow independent web and mobile sessions for the same User.
- Give each session an absolute lifetime of 7 days from login. Rotation does not extend this deadline.
- Issue an opaque refresh token from 32 cryptographically random bytes, rather than another JWT.
- Persist a SHA-256 hash of the refresh token, never its plaintext value. Random high-entropy tokens do not use the password-hashing workflow.
- Rotate the refresh token on each successful refresh, marking the old token consumed and issuing a new token within the same transaction.
- Limit each replacement refresh token to the session's original expiry. Limit access-token expiry to the earlier of 15 minutes from issuance or session expiry.

Retain consumed token hashes until the session expires so reuse can be identified. Reusing a consumed token revokes its session and its remaining refresh tokens. An unknown token is rejected without revoking an unrelated session.

Lock the session row during refresh and coordinate token consumption, replacement insertion, and session revocation in PostgreSQL. The reuse-revocation transaction must commit even though the refresh request is rejected.

Clients must coordinate refresh requests rather than refresh independently for every concurrent API call. Reuse detection can also be triggered by duplicate requests or a lost refresh response; Version 1 requires a new login instead of adding a token-replay grace period.

### Authentication Persistence

| Record | Attributes |
| --- | --- |
| AuthenticationSession | Id, UserId, CreatedAt, ExpiresAt, RevokedAt (nullable) |
| RefreshToken | Id, SessionId, TokenHash, CreatedAt, ExpiresAt, ConsumedAt (nullable) |

Session.UserId references User.Id. RefreshToken.SessionId references AuthenticationSession.Id. TokenHash is unique. A partial unique index on SessionId where ConsumedAt is null limits each session to one unconsumed refresh-token row; expiry and session revocation are validated separately.

Authentication owns these records. They are technical authentication persistence, not scheduling entities or additional User profiles. [AuthenticationModel.md](../AuthenticationModel.md) defines their relational design; final EF Core mappings, migrations, and cleanup execution remain implementation work.

### Current Access and Logout

On every authenticated protected request, load the referenced session and current account/profile authorization data from PostgreSQL. Reject expired or revoked sessions, mismatched session ownership, and Suspended or Deactivated accounts.

PendingVerification patients retain only permitted access. Exact endpoint permissions must be finalized against the existing requirements; a valid JWT does not enable booking or rescheduling for them.

Logout revokes the current session and removes its client credentials. Subsequent requests using its access token are rejected through the session check rather than waiting for JWT expiry. Requests already executing still require each write workflow's normal concurrency validation.

Suspension or deactivation revokes the User's sessions atomically with the status change. Reactivation requires a new login. Current permission checks prevent old role information from granting access after StaffRole changes.

### Client Storage

For React Web, propose keeping the access token in memory and the refresh token in a Secure, HttpOnly cookie. Cookie-authenticated login, refresh, and logout operations require appropriate CSRF defenses. Exact SameSite, cookie scope, credentials, and CORS settings depend on the web/API deployment origins and must be finalized before implementation.

For React Native, keep the access token in memory and the refresh token in OS-backed secure credential storage, not plain application storage. The concrete library will be selected during client implementation.

Use HTTPS and the Authorization bearer header for access tokens. Never place tokens in URLs or logs. The browser obtains account information through the API rather than depending on access-token parsing for authorization.

## Alternatives Considered

- Long-lived access tokens without refresh: fewer endpoints, but a longer usable window for leaked tokens and less control over sessions.
- Self-contained JWT validation without current database checks: cheaper per request, but account, role, and logout changes may not take effect until expiry.
- Raw refresh-token persistence or non-rotating tokens: simpler storage, but exposes usable credentials in a database leak or lacks the selected reuse detection.
- Sliding sessions without an absolute deadline: fewer logins, but sessions can remain valid indefinitely through repeated refresh.
- A web backend-for-frontend keeping all tokens server-side: reduces browser token exposure but adds hosting and request-routing responsibilities beyond the current direct React/API architecture. Reconsider when deployment design is known.

## Consequences

- Login remains valid for at most 7 days while access tokens are refreshed without repeatedly entering credentials.
- Revocation and current permissions work consistently across API instances, using shared PostgreSQL state.
- Protected requests require database authorization reads; JWT authentication does not eliminate shared session state in this design.
- Web access tokens remain exposed to malicious JavaScript while in memory; HttpOnly protects only the refresh cookie from direct JavaScript access. Cookie protection is not a replacement for preventing XSS or CSRF.
- Refresh rotation and revocation require transactional tests, including concurrent refresh, reused tokens, lost responses, logout, expiry, suspension, and role changes.
- Before implementation, finalize signing-key/issuer design, PendingVerification permissions, and client storage/deployment details. The referenced security documents inform this design; this ADR does not claim to implement a complete OAuth/OIDC flow.

## Revisit When

Measured authorization reads become costly, staff require shorter sessions, deployment origins change cookie requirements, or external identity-provider integration becomes necessary.

## References

- [Architecture](../Architecture.md)
- [Functional Requirements](../FunctionalRequirements.md)
- [Non-Functional Requirements](../NonFunctionalRequirements.md)
- [ASP.NET Core JWT bearer validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [OAuth security guidance on refresh-token protection](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14)
- [OWASP session management](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
