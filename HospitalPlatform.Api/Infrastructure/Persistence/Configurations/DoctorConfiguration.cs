using HospitalPlatform.Api.Features.Doctors.Models;
using HospitalPlatform.Api.Features.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalPlatform.Api.Infrastructure.Persistence.Configurations;

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors", table =>
            table.HasCheckConstraint("CK_Doctors_ProfileType", "\"ProfileType\" = 'Doctor'"));

        builder.HasKey(doctor => doctor.Id);
        builder.Property(doctor => doctor.Id).ValueGeneratedNever();
        builder.HasIndex(doctor => doctor.UserId).IsUnique();
        builder.HasIndex(doctor => doctor.EmployeeNumber).IsUnique();
        builder.HasIndex(doctor => doctor.MedicalLicenseNumber).IsUnique();

        builder.Property(doctor => doctor.ProfileType)
            .HasConversion<string>()
            .HasDefaultValue(ProfileType.Doctor);

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<Doctor>("UserId", "ProfileType")
            .HasPrincipalKey<User>(user => new { user.Id, user.ProfileType })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EmployeeNumberRegistration>()
            .WithOne()
            .HasForeignKey<Doctor>(doctor => new { doctor.EmployeeNumber, doctor.UserId })
            .HasPrincipalKey<EmployeeNumberRegistration>(registration => new { registration.EmployeeNumber, registration.UserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
