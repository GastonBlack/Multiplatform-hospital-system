using HospitalPlatform.Api.Features.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalPlatform.Api.Infrastructure.Persistence.Configurations;

public class EmployeeNumberRegistrationConfiguration : IEntityTypeConfiguration<EmployeeNumberRegistration>
{
    public void Configure(EntityTypeBuilder<EmployeeNumberRegistration> builder)
    {
        builder.ToTable("EmployeeNumbers");
        builder.HasKey(registration => registration.EmployeeNumber);
        builder.HasIndex(registration => registration.UserId).IsUnique();
        builder.HasAlternateKey(registration => new { registration.EmployeeNumber, registration.UserId });

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<EmployeeNumberRegistration>(registration => registration.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
