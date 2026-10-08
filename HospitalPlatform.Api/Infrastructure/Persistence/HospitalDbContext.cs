using HospitalPlatform.Api.Features.Patients.Models;
using HospitalPlatform.Api.Features.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalPlatform.Api.Infrastructure.Persistence;

public class HospitalDbContext(DbContextOptions<HospitalDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);
    }
}
