# Tests

All automated tests live in the `HospitalPlatform.Api.Tests` project and are grouped first by test type, then by feature:

```text
HospitalPlatform.Api.Tests/
  Unit/
    Features/
      Users/EmailNormalizerTests.cs
  Integration/
    Features/
      Authentication/AuthenticationTests.cs
      Patients/PatientRegistrationTests.cs
      Patients/PatientProfileTests.cs
      Staff/StaffRegistrationTests.cs
      Users/UserProfileTests.cs
    Infrastructure/
      DatabaseSeedTests.cs
      OpenApiTests.cs
  Functional/
    Features/
      Staff/StaffRegistrationFlowTests.cs
  Shared/
    Infrastructure/
      HospitalApiFactory.cs
      AuthorizationProbeController.cs
```

- **Unit:** isolated logic, with no HTTP host or database. Email normalization tests cover canonical addresses, rejected input, preserved dots/plus suffixes, and culture-independent casing.
- **Integration:** API, authentication, EF Core, and PostgreSQL working together, including persisted state, constraints, rollback, and concurrency. OpenAPI tests check host configuration without opening a database connection.
- **Functional:** complete user workflows through production HTTP endpoints. The staff workflow covers administrator login, employee registration, employee login, own-profile reads, assigned permissions, and logout. Database access is limited to test setup and cleanup.
- **Shared:** reusable test infrastructure, without test cases. The authorization probe is registered only in the test host and is used by integration tests.

Use matching namespaces such as `HospitalPlatform.Api.Tests.Unit.Features.Users`. Add feature tests under the appropriate type; keep cross-cutting infrastructure tests in `Integration/Infrastructure`. A test that starts the API or connects to PostgreSQL is not a unit test. Functional tests run against the in-process API and do not automate a browser or mobile client.

## Run all tests

From the repository root, start PostgreSQL and run the solution:

```powershell
docker compose up -d --wait
dotnet test --solution HospitalPlatform.sln --configuration Release
```

Tests that use PostgreSQL create and remove a separate `hospital_test_<UUID>` database for each case. They read server credentials from `HOSPITAL_TEST_CONNECTION_STRING` or the API's development connection secret, then replace the database name. They never seed or truncate the development database. The database user must be allowed to create and drop test databases. See [development configuration](../docs/Development.md).

## Run one test type

Unit tests do not require Docker or database credentials:

```powershell
dotnet test --project tests/HospitalPlatform.Api.Tests/HospitalPlatform.Api.Tests.csproj --configuration Release -- --filter-class 'HospitalPlatform.Api.Tests.Unit.*'
```

Integration and functional tests require PostgreSQL:

```powershell
dotnet test --project tests/HospitalPlatform.Api.Tests/HospitalPlatform.Api.Tests.csproj --configuration Release -- --filter-class 'HospitalPlatform.Api.Tests.Integration.*'
dotnet test --project tests/HospitalPlatform.Api.Tests/HospitalPlatform.Api.Tests.csproj --configuration Release -- --filter-class 'HospitalPlatform.Api.Tests.Functional.*'
```

To select a feature, narrow the same class-name pattern, for example `HospitalPlatform.Api.Tests.Integration.Features.Patients.*`. The options after `--` are xUnit filters forwarded by Microsoft.Testing.Platform, selected in `global.json`. GitHub Actions runs all three types with the solution command.
