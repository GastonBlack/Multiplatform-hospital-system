# Local Development

## Current Persistence Setup

The API runs locally with .NET 10. PostgreSQL 18.6 runs through the root compose.yaml. EF Core uses the Npgsql provider and a scoped HospitalDbContext, registered in Program.cs.

HospitalDbContext exposes Users and Patients and loads their IEntityTypeConfiguration mappings from Infrastructure/Persistence/Configurations. User enums are stored as constrained text; email and account identification numbers are unique across all profiles. NationalIdentificationNumber now belongs to User; the new migration copies existing patient identifiers before removing their old column. Patient has a unique UserId and a composite account/profile-type foreign key. Its fixed ProfileType is initialized to Patient with a private setter so EF can order account/profile inserts together. It is a persistence discriminator, not an independently editable profile field. Verification fields must be absent or populated together, and referenced accounts use restrictive deletion.

HospitalDbContext also exposes Doctors, Staff, and EmployeeNumbers. Doctor and Staff use fixed profile discriminators and unique UserId values. EmployeeNumbers has EmployeeNumber as its primary key, a unique UserId, and an alternate composite key (EmployeeNumber, UserId). Employee profile foreign keys reference that pair so a profile cannot use another account's employee number. Medical licenses are unique, StaffRole is constrained text, and these relationships use restrictive deletion.

The InitialAccounts migration creates these five tables, their constraints, and EmployeeNumberSequence (bigint, starting at 1, incrementing by 1, without cycling). Doctor registration, Staff registration, and the initial administrator seed allocate and format employee numbers through this shared sequence.

Deferred constraint triggers require a matching profile when a transaction commits. Immediate keys prevent multiple or mismatched profiles. Profile mutations lock the affected User rows in UUID order before changing the profile; deferred checks read the final state under READ COMMITTED. Account/profile creation must use one transaction, including the employee registry for Doctor and Staff. Validation can fail at commit even after SaveChanges succeeds. TRUNCATE is rejected on all five tables. Production application credentials must not own the schema or have privileges to disable triggers.

Migration validation used a disposable PostgreSQL database: valid patient/doctor/staff creation across separate SaveChanges calls, missing profiles, profile deletion and ownership changes, rollback, deactivation preserving profiles, conflicting concurrent writes, sequence gaps, TRUNCATE rejection, and Down/Up round-trip. This was a temporary integration probe, not a committed test suite or a CI job.

Public patient registration is implemented in PatientRegistrationService, behind IPatientRegistrationService and registered as scoped in Program.cs. RegisterAsync validates RegisterPatientRequest, canonicalizes email through the shared EmailNormalizer, and hashes the unchanged password using ASP.NET Core's PasswordHasher<User>. It creates User with ProfileType.Patient and PendingVerification plus an unverified Patient in one explicit transaction. Both timestamps are assigned in UTC; identification is stored on User, its input remains unchanged and must contain exactly eight ASCII digits. PostgreSQL uniqueness violations for email and identification become ConflictException errors with a message identifying the duplicate; other persistence failures propagate and transaction disposal rolls back uncommitted data. The response contains only UserId, PatientId, and AccountStatus and is returned after commit.

ExceptionHandlingMiddleware is registered before the remaining HTTP middleware. Custom AppException types carry the expected status and error code; ValidationException maps to 400 and DbUpdateConcurrencyException maps to 409. Unknown errors, including other DbUpdateException, InvalidOperationException, and ArgumentException failures, map to 500. ErrorResponse uses camelCase JSON and includes error, message, statusCode, traceId, and a UTC timestamp. Unexpected exception messages are exposed only in Development; Production returns a generic message. Errors are logged, and an already-started response is not overwritten.

PatientsController exposes public POST /api/patients/register and calls IPatientRegistrationService. Successful registration returns 201 with UserId, PatientId, and AccountStatus, without passwords, hashes, or tokens. AccountStatus currently uses the default numeric enum serialization (1 means PendingVerification). ApiController validates request DTOs and returns automatic 400 ValidationProblemDetails responses for invalid input or malformed JSON before invoking the service. Known duplicates become 409 ErrorResponse responses through the exception middleware. These are different error-body shapes; clients should use the HTTP status and the corresponding response fields. Receptionist-created patient registration remains a subsequent implementation step. Registration does not automatically log the patient in. Do not use EnsureCreated. Check-digit arithmetic remains out of scope.

Example request body:

```json
{
  "firstName": "Ana",
  "lastName": "Perez",
  "email": "ana@example.com",
  "password": "Ab1!_?xy",
  "nationalIdentificationNumber": "01234567",
  "phoneNumber": "+598 99 123 456",
  "dateOfBirth": "2000-01-01"
}
```

A temporary PostgreSQL integration probe verified committed registration data, unchanged password verification, different salted hashes for equal passwords, duplicate email/identification errors, invalid input, full rollback after a deferred commit failure, and competing registrations with the same email. That probe was temporary; the committed registration test suite is described below.

A temporary HTTP/PostgreSQL probe also verified 20 requests through the real PatientsController, service, and exception middleware: anonymous 201 registration, automatic 400 responses for invalid or missing fields and malformed JSON, duplicate email/identification 409 responses, safe response fields, server-owned profile/status/verification values, and concurrent same-email registration with exactly one committed account/profile pair. The disposable database and HTTP server were removed after testing; the application database was not modified. That probe was temporary; CI now runs the committed registration tests described below.

## PostgreSQL

Docker Desktop must be running with Linux containers. On a fresh checkout, copy .env.example to .env and replace its placeholder with a local development password. Existing local .env files should be preserved.

```powershell
Copy-Item .env.example .env
```

Start and inspect the database from the repository root:

```powershell
docker compose up -d --wait
docker compose ps
```

The postgres service publishes host port 15432 only on 127.0.0.1, forwarding to PostgreSQL port 5432 inside the container. Host port 5432 could not be bound on the initial development machine, so this project uses 15432. Its database is hospital_platform and its local development user is hospital_dev. The health check uses pg_isready to check readiness.

The postgres_data named volume stores PostgreSQL data at /var/lib/postgresql, the volume location for PostgreSQL 18. Stopping the container preserves this volume:

```powershell
docker compose stop
```

Changing POSTGRES_PASSWORD in .env does not change a password in an already initialized database. Keep existing credentials consistent rather than deleting the data volume.

## API Connection

The local .env is consumed by Compose; ASP.NET Core does not load it automatically. The API reads ConnectionStrings:HospitalDatabase from its normal configuration sources. In Development, use user-secrets for the connection string. This keeps local credentials outside the repository; user-secrets is development storage, not a production secret vault.

Use the same password as .env:

```powershell
dotnet user-secrets set "ConnectionStrings:HospitalDatabase" "Host=localhost;Port=15432;Database=hospital_platform;Username=hospital_dev;Password=<password-from-.env>" --project HospitalPlatform.Api
dotnet run --project HospitalPlatform.Api
```

Program.cs selects UseNpgsql through AddDbContext. HospitalDbContext receives its configured options through dependency injection. Each request receives a scoped context; do not use one context concurrently across operations.

Program.cs groups dependency registration into Database, Feature Services, Authentication, Authorization, and API Services sections before builder.Build(). Register new feature services in Feature Services. After Build, the HTTP pipeline handles exceptions, redirects to HTTPS, authenticates, and authorizes requests in that order; MapControllers exposes the controller endpoints.

Timestamp instants must be assigned in UTC. Hospital-local display and scheduling rules use America/Montevideo. DateOfBirth remains DateOnly.

## Swagger UI

Start the API with the HTTPS development profile:

```powershell
dotnet run --project HospitalPlatform.Api --launch-profile https
```

This profile opens https://localhost:7109/swagger. Swagger UI uses the existing ASP.NET Core OpenAPI document at /openapi/v1.json. Both the UI and the document are exposed only in Development.

Use POST /api/auth/login with an existing account, copy accessToken from the response, and paste only that token into the Authorize dialog. Swagger adds the Authorization: Bearer header when calling protected operations. Registration, login, and refresh remain anonymous in the document; refresh still accepts refreshToken in its JSON request body. API authorization, session revocation, and account-state rules continue to apply to requests made through Swagger.

Integration/Infrastructure/OpenApiTests.cs verifies the UI, JavaScript assets, and document are available only in Development, and that JWT bearer requirements are documented only on protected endpoints. These tests supply isolated configuration and require no PostgreSQL connection.

## Migrations

Restore the repository-local EF tool on a fresh checkout, then apply pending migrations after configuring the connection and starting PostgreSQL:

```powershell
dotnet tool restore
dotnet ef database update --project HospitalPlatform.Api -- --environment Development
```

EF records applied migrations in __EFMigrationsHistory, so running the command again does not recreate tables. Migrations and HospitalDbContextModelSnapshot are versioned source files. Up applies a schema change; Down reverses it. Reversing InitialAccounts drops the account/profile tables and their data, so do not use it on a populated application database.

For a future model change, generate and review a new migration before applying it:

```powershell
dotnet ef migrations add <DescriptiveName> --project HospitalPlatform.Api --output-dir Infrastructure/Persistence/Migrations -- --environment Development
```

Custom trigger SQL lives in the migration; EF does not generate it from entity mappings. Future changes to these guarantees require an explicit migration. The API does not automatically migrate on startup.

## Initial Administrator Seed

DatabaseSeed in Infrastructure/Persistence/Seeds coordinates initial data in one transaction. Feature seeds live under Features/Users/Seeds/UserSeed.cs and Features/Staff/Seeds/StaffSeed.cs. UserSeed creates the account, EmployeeNumberService registers its sequence-generated employee number, and StaffSeed creates the matching Administrator profile. Entity configurations and OnModelCreating only define the schema, without inserting accounts or generating password hashes.

Configure the initial password in development user-secrets, replacing the placeholder with your chosen password of 6-12 characters, matching LoginRequest:

```powershell
dotnet user-secrets set "DatabaseSeed:Administrator:Password" "<your-local-administrator-password>" --project HospitalPlatform.Api
dotnet user-secrets set "DatabaseSeed:Administrator:NationalIdentificationNumber" "<your-eight-digit-identification>" --project HospitalPlatform.Api
```

With PostgreSQL running and the existing connection configuration in place, execute:

```powershell
dotnet run --project HospitalPlatform.Api -- --environment Development --seed true
```

This explicit initialization command applies pending migrations, executes DatabaseSeed.SeedAsync, and exits without starting the HTTP server. Normal API startup does not migrate or seed the database. Other environments must supply the seed password through protected configuration rather than development user-secrets.

The initial account uses FirstName Default, LastName Administrator, email administrator0101@gmail.com, ProfileType Staff, AccountStatus Active, and StaffRole Administrator. The login identification is supplied through DatabaseSeed:Administrator:NationalIdentificationNumber and must contain exactly eight ASCII digits. PasswordHasher<User> hashes the supplied password; no plaintext password or fixed hash is stored in source. EmployeeNumber comes from EmployeeNumberSequence and is formatted as EMP- plus at least six decimal digits.

If any Administrator profile already exists, seeding leaves accounts, credentials, statuses, and employee numbers unchanged. A transaction-scoped Staff table lock serializes concurrent initialization. Account, employee registration, and Staff profile commit together; uniqueness or deferred-constraint failures roll the entire seed back. The seed never converts an existing patient or doctor account to administrator.

Integration/Infrastructure/DatabaseSeedTests.cs covers concurrent and repeated runs, preserved credentials and account state, login and administrator authorization with the generated account, rejected passwords, sequence values beyond six digits, and rollback when the seed email belongs to a patient. Tests use isolated databases and never seed hospital_platform.

## Staff Registration

POST /api/staff/register creates receptionists and administrators through StaffController and IStaffRegistrationService. It requires a valid JWT, the current Administrator role, and an Active account under the default authorization policy. Missing or invalid authentication returns 401; other roles and non-active administrators receive 403. Roles and account state are read from PostgreSQL on every protected request, including requests using an older JWT.

In Swagger, log in with an active administrator and paste the accessToken into Authorize before sending this request:

```json
{
  "firstName": "Ana",
  "lastName": "Perez",
  "email": "ana.reception@example.com",
  "nationalIdentificationNumber": "01234567",
  "password": "Ab1!_?xy",
  "staffRole": "Receptionist"
}
```

Use Receptionist or Administrator for staffRole. Names require 2-120 characters, email at most 150 characters and the shared normalization policy, and password 6-12 characters without trimming. Invalid roles, missing fields, and invalid input return 400. A duplicate email or identification number across any profile returns 409. New staff accounts require nationalIdentificationNumber with exactly eight ASCII digits.

The service creates an Active User with ProfileType Staff, hashes the password with PasswordHasher<User>, and saves the account, EmployeeNumbers registration, and Staff profile in one transaction. EmployeeNumberService in Features/Users/Services is shared with the initial administrator seed and allocates numbers from EmployeeNumberSequence. Sequence values are padded to at least six digits, without truncation; rolled-back allocations may leave gaps. Clients cannot assign identifiers, employee numbers, profile types, or account states.

The 201 response contains userId, staffId, employeeNumber, staffRole, and accountStatus. Role and account status are text; passwords, hashes, and tokens are omitted. The new employee can then use POST /api/auth/login and GET /api/users/me. Apply UserNationalIdentificationLogin before using identification-based registration and login.

Integration/Features/Staff/StaffRegistrationTests.cs verifies persisted creation for both roles, permissions and current authorization changes, validation, server-owned values, duplicate-email rollback without recycling the number, concurrent duplicate emails, and concurrent staff/doctor number allocation beyond six digits. Functional/Features/Staff/StaffRegistrationFlowTests.cs verifies registration, employee login, own-profile reads, assigned permissions, and logout through production HTTP endpoints. The shared-number test uses a doctor fixture to isolate sequence allocation. These tests use only the isolated databases created by HospitalApiFactory.

## Doctor Registration

POST /api/doctors/register creates doctor accounts through DoctorsController and IDoctorRegistrationService. It requires a valid JWT, the current Administrator role, and an Active account. The controller combines the default active-account policy with the administrator role restriction. Anonymous or invalid-token requests return 401; other roles and non-active administrators receive 403.

Log in as an active administrator, paste the accessToken into Swagger's Authorize dialog, and submit:

```json
{
  "firstName": "Maria",
  "lastName": "Gomez",
  "email": "medico@example.com",
  "nationalIdentificationNumber": "12345678",
  "password": "Ab1!_?xy",
  "medicalLicenseNumber": "00012345"
}
```

Names require 2-120 characters, email at most 150 characters and the shared normalization policy, and password 6-12 characters without trimming. MedicalLicenseNumber is required text: exterior whitespace is trimmed, while letter case and leading zeros are preserved. No numeric-only format or license checksum is imposed. Invalid or missing data returns 400. Duplicate email or identification across any account profile, or duplicate medical license, returns 409. New doctor accounts require nationalIdentificationNumber with exactly eight ASCII digits. Suspended and Deactivated doctors retain ownership of their licenses.

The service hashes the password and creates an Active User with ProfileType Doctor, an EmployeeNumbers registration through the shared EmployeeNumberService, and a Doctor profile in one transaction. Failed registrations leave no partial account, number registration, or profile; an allocated sequence value may leave a gap. Identifiers, employee numbers, profile type, and account state are server-owned. The Doctor model and license index already support this operation; apply UserNationalIdentificationLogin for the shared account identification column.

The 201 response contains userId, doctorId, employeeNumber, medicalLicenseNumber, and accountStatus as text. It does not return passwords, hashes, or tokens. The doctor can log in through POST /api/auth/login and read basic account data through GET /api/users/me. Registration does not assign specialties or availability.

Integration/Features/Doctors/DoctorRegistrationTests.cs verifies persisted ownership and password hashes, authorization, validation, duplicate-email/license rollback, reserved licenses on inactive accounts, server-owned fields, concurrent duplicate requests, and simultaneous Doctor/Staff registration using the same sequence beyond six digits. Functional/Features/Doctors/DoctorRegistrationFlowTests.cs exercises administrator login, doctor registration, doctor login, own-account reads, denied administrator operations, and logout through HTTP endpoints.

## Identification Login and Frontend Contract

All roles log in with POST /api/auth/login using nationalIdentificationNumber and password:

```json
{
  "nationalIdentificationNumber": "01234567",
  "password": "Ab1!_?xy"
}
```

The identification number is a string of exactly eight ASCII digits. Preserve leading zeros and reject punctuation, whitespace, missing digits, and numeric JSON values. Do not trim, pad, or calculate the check digit. Email-only login is no longer supported. Invalid input returns 400; unknown identification or incorrect password returns the same generic 401 error. Token responses, JWT identity claims, refresh, logout, and account-status permissions are unchanged.

POST /api/staff/register and POST /api/doctors/register now also require nationalIdentificationNumber. POST /api/patients/register already required it and keeps its existing body. All three create identifiers on User, with a database uniqueness constraint shared across every role. Email is still required and unique for contact. GET /api/users/me keeps its existing response; GET /api/patients/me still returns nationalIdentificationNumber, now projected from User.

The UserNationalIdentificationLogin migration copies existing patient identifiers to User before removing Patients.NationalIdentificationNumber. Existing employee accounts are retained with NULL identifiers until their actual values are supplied; no placeholder is assigned. New registration and fresh administrator seeding require an identifier. An unmapped legacy employee cannot start a new login, while existing valid sessions retain the established session rules. Backfill verified identification values before asking those employees to log in. Migration rollback restores patient identifiers to Patients; the old schema cannot retain newly supplied employee identifiers, so preserve them separately before a downgrade.

Apply the migration after setting the development connection, with PostgreSQL running:

```powershell
dotnet ef database update --project HospitalPlatform.Api --configuration Release -- --environment Development
```

For the frontend: replace the login email field with a text input named nationalIdentificationNumber (inputMode="numeric", maxLength=8), update LoginRequest, and add the same required string field to staff/doctor registration. Keep the email field in registration forms. Do not store the identification number in JWTs or send it as the authorization identity for other endpoints: protected endpoints continue using the validated UUID sub claim.

## Authentication

AuthenticationService implements POST /api/auth/login and POST /api/auth/refresh; AuthenticationController also exposes protected POST /api/auth/logout and GET /api/auth/session. Login returns accessToken, refreshToken, accessTokenExpiresAt, and sessionExpiresAt. Refresh accepts the exact refreshToken in a JSON body and replaces it. Logout returns 204 and revokes the current session. Send access tokens only in the Authorization: Bearer header over HTTPS.

The session response preserves userId and sessionId as UUID strings and profileType and accountStatus as numeric enums, and adds role as text from the current validated principal (Patient, Doctor, Receptionist, or Administrator; null if the role claim is absent). AuthenticatedAccount allows session reads in every account state. Administrative access requires both Administrator and Active, enforced by the API on every protected operation. See the [session contract](AuthenticationModel.md#6-backend-implementation).

Credentials and valid sessions are accepted for every account state, including PendingVerification, Suspended, and Deactivated. AuthenticatedAccount permits authentication lifecycle operations and read-only own-account access. The default policy still requires Active for other protected operations. JWT signature, issuer, audience, expiry, session ownership, and revocation checks remain enforced; current roles and status come from PostgreSQL.

GET /api/users/me returns the caller's UserId, FirstName, LastName, Email, ProfileType, and AccountStatus for any profile or account state. ProfileType and AccountStatus are text. GET /api/patients/me requires the Patient role and returns the caller's patient data and AccountStatus as text in every state. Both derive User.Id from the validated sub claim instead of accepting a user ID. PatientsController delegates to IPatientRegistrationService.GetPatientProfileAsync; UsersController delegates to IUserProfileService.GetProfileAsync. Neither response contains password data.

To inspect the current session, send the following request to the API's HTTPS address, replacing `<accessToken>` with the value returned by login:

```http
GET /api/auth/session HTTP/1.1
Authorization: Bearer <accessToken>
```

A successful response returns userId, sessionId, profileType, and accountStatus. No user ID is needed in the URL: the API obtains it from the validated access token.

AuthenticationSessions creates AuthenticationSessions and RefreshTokens, with expiry, hash-length, ownership, and one-unconsumed-token constraints. Sessions expire seven days after login; JWTs expire after at most fifteen minutes and never outlive their session. Reuse revokes the session even though refresh returns 401. User locks precede session locks; no external call occurs inside these transactions. Authentication middleware runs before authorization and checks the current session/account state on every protected request.

appsettings.json contains the non-secret issuer and audience. The API also requires Authentication:SigningKey with at least 32 characters. The initial development key was generated cryptographically and stored in user-secrets. A fresh developer checkout must generate its own key and set it without committing it:

```powershell
$keyBytes = New-Object byte[] 32
$generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$generator.GetBytes($keyBytes)
$generator.Dispose()
$signingKey = [BitConverter]::ToString($keyBytes).Replace('-', '')
dotnet user-secrets set "Authentication:SigningKey" $signingKey --project HospitalPlatform.Api
```

Preserve an existing development key instead of regenerating it on every run. Production supplies the key through protected configuration. Tests override it with a per-host random key, so CI does not require a signing secret. Browser refresh cookies, deployment-origin/CSRF decisions, production key rotation, and expired-session cleanup remain pending; current JSON transport is for API/native development. Future suspension/deactivation workflows must revoke all sessions in their status-change transaction.

## Build and CI

```powershell
dotnet restore HospitalPlatform.sln
dotnet build HospitalPlatform.sln --configuration Release --no-restore
dotnet test --solution HospitalPlatform.sln --configuration Release --no-build --no-restore
```

The CI job starts a PostgreSQL 18.6 service container, restores dependencies, builds the solution, and runs unit, integration, and functional tests. Its hardcoded database credentials belong only to the disposable test container; they are not application credentials. The GitHub run must still be checked after pushing.

## Automated Tests

All suites live in tests/HospitalPlatform.Api.Tests, organized first by type and then by feature. See [tests/README.md](../tests/README.md) for the directory tree, classification rules, and commands to run one type or feature.

Unit/Features contains isolated tests without an HTTP host or database; Users/EmailNormalizerTests.cs verifies the email normalization policy and Users/NationalIdentificationNumberAttributeTests.cs verifies the shared identification format. Integration/Features contains Authentication, Doctors, Patients, Staff, and Users tests that exercise the real API and persisted state. Integration/Infrastructure covers database seeding and OpenAPI configuration. Functional/Features contains complete Doctor and Staff registration, login, own-account reads, permission, and logout workflows, without querying persistence inside the scenario.

Shared/Infrastructure/HospitalApiFactory.cs owns the reusable host, isolated signing key, and database lifecycle. AuthorizationProbeController is loaded only by the test host for integration authorization checks. WebApplicationFactory starts the real Program entry point in Testing with the generated database; the public partial Program declaration exposes that entry point. The HTTPS client address avoids testing redirects instead of endpoint behavior. xUnit (xunit.v3) runs through Microsoft.Testing.Platform, selected in global.json.

Each database-backed case creates a separate hospital_test_<random UUID> database, applies the actual EF migrations, and removes that database after stopping its host. Tests never seed or truncate hospital_platform. Start Docker Desktop and run docker compose up -d --wait before integration or functional tests. HOSPITAL_TEST_CONNECTION_STRING overrides the API's development connection secret; server credentials must allow creating and dropping databases. Signing keys are generated per test host, so CI does not need a signing secret. Unit tests do not require Docker or database credentials.

Fact marks one case; Theory plus InlineData runs multiple cases. Namespace-based class filters select Unit, Integration, Functional, or a feature without changing the test runner. Only the existing xUnit1051 cancellation-token recommendation is suppressed. GitHub Actions builds the solution and runs all test types after push.

To run everything from the repository root:

```powershell
docker compose up -d --wait
dotnet test --solution HospitalPlatform.sln --configuration Release
```

## References

- [DbContext configuration and lifetime](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [ASP.NET Core development secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [PostgreSQL official Docker image](https://hub.docker.com/_/postgres)
