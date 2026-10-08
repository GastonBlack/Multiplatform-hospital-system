using HospitalPlatform.Api.Features.Patients.Models;
using HospitalPlatform.Api.Features.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalPlatform.Api.Infrastructure.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients", table =>
        {
            table.HasCheckConstraint("CK_Patients_ProfileType", "\"ProfileType\" = 'Patient'");
            table.HasCheckConstraint("CK_Patients_NationalIdentificationNumber",
                "\"NationalIdentificationNumber\" ~ '^[0-9]{8}$'");
            table.HasCheckConstraint("CK_Patients_IdentityVerification",
                "(\"IdentityVerifiedAt\" IS NULL AND \"IdentityVerifiedByUserId\" IS NULL) OR " +
                "(\"IdentityVerifiedAt\" IS NOT NULL AND \"IdentityVerifiedByUserId\" IS NOT NULL)");
        });

        builder.HasKey(patient => patient.Id);
        builder.Property(patient => patient.Id).ValueGeneratedNever();
        builder.HasIndex(patient => patient.UserId).IsUnique();
        builder.HasIndex(patient => patient.NationalIdentificationNumber).IsUnique();

        builder.Property(patient => patient.ProfileType)
            .HasConversion<string>()
            .HasDefaultValue(ProfileType.Patient)
            .HasSentinel(ProfileType.Patient);

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<Patient>("UserId", "ProfileType")
            .HasPrincipalKey<User>(user => new { user.Id, user.ProfileType })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(patient => patient.IdentityVerifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(patient => patient.DateOfBirth).HasColumnType("date");
        builder.Property(patient => patient.IdentityVerifiedAt).HasColumnType("timestamp with time zone");
    }
}
