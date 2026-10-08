# Local Development

## Current Persistence Setup

The API runs locally with .NET 10. PostgreSQL 18.6 runs through the root compose.yaml. EF Core uses the Npgsql provider and a scoped HospitalDbContext, registered in Program.cs.

HospitalDbContext currently exposes Users and Patients. This is the initial connection setup: explicit entity mappings, profile constraints, migrations, and registration workflows are subsequent implementation steps. Do not use EnsureCreated or apply an initial schema based only on conventions.

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