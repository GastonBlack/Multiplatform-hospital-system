using HospitalPlatform.Api.Features.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalPlatform.Api.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", table =>
        {
            table.HasCheckConstraint("CK_Users_ProfileType",
                "\"ProfileType\" IN ('Patient', 'Doctor', 'Staff')");
            table.HasCheckConstraint("CK_Users_AccountStatus",
                "\"AccountStatus\" IN ('PendingVerification', 'Active', 'Suspended', 'Deactivated')");
        });

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.HasAlternateKey(user => new { user.Id, user.ProfileType });
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.ProfileType).HasConversion<string>();
        builder.Property(user => user.AccountStatus).HasConversion<string>();
        builder.Property(user => user.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(user => user.UpdatedAt).HasColumnType("timestamp with time zone");
    }
}
