# Local Development

## Current Persistence Setup

The API runs locally with .NET 10. PostgreSQL 18.6 runs through the root compose.yaml. EF Core uses the Npgsql provider and a scoped HospitalDbContext, registered in Program.cs.

HospitalDbContext exposes Users and Patients and loads their IEntityTypeConfiguration mappings from Infrastructure/Persistence/Configurations. User enums are stored as constrained text; email and patient identification numbers are unique. Patient has a unique UserId and a composite account/profile-type foreign key. Its fixed ProfileType is initialized to Patient with a private setter so EF can order account/profile inserts together. It is a persistence discriminator, not an independently editable profile field. Verification fields must be absent or populated together, and referenced accounts use restrictive deletion.

HospitalDbContext also exposes Doctors, Staff, and EmployeeNumbers. Doctor and Staff use fixed profile discriminators and unique UserId values. EmployeeNumbers has EmployeeNumber as its primary key, a unique UserId, and an alternate composite key (EmployeeNumber, UserId). Employee profile foreign keys reference that pair so a profile cannot use another account's employee number. Medical licenses are unique, StaffRole is constrained text, and these relationships use restrictive deletion.

The InitialAccounts migration creates these five tables, their constraints, and EmployeeNumberSequence (bigint, starting at 1, incrementing by 1, without cycling). Number allocation and canonical formatting still belong to the future registration workflow; the sequence alone does not assign EmployeeNumber values.

Deferred constraint triggers require a matching profile when a transaction commits. Immediate keys prevent multiple or mismatched profiles. Profile mutations lock the affected User rows in UUID order before changing the profile; deferred checks read the final state under READ COMMITTED. Account/profile creation must use one transaction, including the employee registry for Doctor and Staff. Validation can fail at commit even after SaveChanges succeeds. TRUNCATE is rejected on all five tables. Production application credentials must not own the schema or have privileges to disable triggers.

Migration validation used a disposable PostgreSQL database: valid patient/doctor/staff creation across separate SaveChanges calls, missing profiles, profile deletion and ownership changes, rollback, deactivation preserving profiles, conflicting concurrent writes, sequence gaps, TRUNCATE rejection, and Down/Up round-trip. This was a temporary integration probe, not a committed test suite or a CI job.

Public patient registration is implemented in PatientRegistrationService, behind IPatientRegistrationService and registered as scoped in Program.cs. RegisterAsync validates RegisterPatientRequest, canonicalizes email through the shared EmailNormalizer, and hashes the unchanged password using ASP.NET Core's PasswordHasher<User>. It creates User with ProfileType.Patient and PendingVerification plus an unverified Patient in one explicit transaction. Both timestamps are assigned in UTC; identification input remains unchanged and must contain exactly eight ASCII digits. PostgreSQL uniqueness violations for email and identification become ConflictException errors with a message identifying the duplicate; other persistence failures propagate and transaction disposal rolls back uncommitted data. The response contains only UserId, PatientId, and AccountStatus and is returned after commit.

ExceptionHandlingMiddleware is registered before the remaining HTTP middleware. Custom AppException types carry the expected status and error code; ValidationException maps to 400 and DbUpdateConcurrencyException maps to 409. Unknown errors, including other DbUpdateException, InvalidOperationException, and ArgumentException failures, map to 500. ErrorResponse uses camelCase JSON and includes error, message, statusCode, traceId, and a UTC timestamp. Unexpected exception messages are exposed only in Development; Production returns a generic message. Errors are logged, and an already-started response is not overwritten.

The registration controller, login, and receptionist/employee registration workflows remain subsequent implementation steps. No registration endpoint or automatic login is introduced by the service. Do not use EnsureCreated. Check-digit arithmetic remains out of scope.

A temporary PostgreSQL integration probe verified committed registration data, unchanged password verification, different salted hashes for equal passwords, duplicate email/identification errors, invalid input, full rollback after a deferred commit failure, and competing registrations with the same email. These service checks are not yet a committed test suite or part of CI.

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

Timestamp instants must be assigned in UTC. Hospital-local display and scheduling rules use America/Montevideo. DateOfBirth remains DateOnly.

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

## Build and CI

```powershell
dotnet restore HospitalPlatform.sln
dotnet build HospitalPlatform.sln --configuration Release --no-restore
```

The current CI restores and builds the solution and does not require a running database. Database integration tests will add a PostgreSQL service when introduced.

## References

- [DbContext configuration and lifetime](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [ASP.NET Core development secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [PostgreSQL official Docker image](https://hub.docker.com/_/postgres)
