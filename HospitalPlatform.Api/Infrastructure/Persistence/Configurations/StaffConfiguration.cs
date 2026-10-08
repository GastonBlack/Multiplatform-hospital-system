using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Features.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalPlatform.Api.Infrastructure.Persistence.Configurations;

public class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff", table =>
        {
            table.HasCheckConstraint("CK_Staff_ProfileType", "\"ProfileType\" = 'Staff'");
            table.HasCheckConstraint("CK_Staff_StaffRole", "\"StaffRole\" IN ('Receptionist', 'Administrator')");
        });

        builder.HasKey(staff => staff.Id);
        builder.Property(staff => staff.Id).ValueGeneratedNever();
        builder.HasIndex(staff => staff.UserId).IsUnique();
        builder.HasIndex(staff => staff.EmployeeNumber).IsUnique();

        builder.Property(staff => staff.ProfileType)
            .HasConversion<string>()
            .HasDefaultValue(ProfileType.Staff)
            .HasSentinel(ProfileType.Staff);
        builder.Property(staff => staff.StaffRole).HasConversion<string>();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<Staff>("UserId", "ProfileType")
            .HasPrincipalKey<User>(user => new { user.Id, user.ProfileType })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EmployeeNumberRegistration>()
            .WithOne()
            .HasForeignKey<Staff>(staff => new { staff.EmployeeNumber, staff.UserId })
            .HasPrincipalKey<EmployeeNumberRegistration>(registration => new { registration.EmployeeNumber, registration.UserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
