using HospitalPlatform.Api.Features.Doctors.Models;
using HospitalPlatform.Api.Features.Patients.Models;
using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalPlatform.Api.Infrastructure.Persistence;

public class HospitalDbContext(DbContextOptions<HospitalDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<EmployeeNumberRegistration> EmployeeNumbers => Set<EmployeeNumberRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);
    }
}
