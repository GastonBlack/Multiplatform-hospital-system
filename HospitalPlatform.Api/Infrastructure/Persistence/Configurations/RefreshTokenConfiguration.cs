using HospitalPlatform.Api.Features.Authentication.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalPlatform.Api.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", table =>
        {
            table.HasCheckConstraint("CK_RefreshTokens_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CK_RefreshTokens_Hash", "octet_length(\"TokenHash\") = 32");
        });
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.SessionId).IsUnique().HasFilter("\"ConsumedAt\" IS NULL");
        builder.HasOne<AuthenticationSession>().WithMany().HasForeignKey(token => token.SessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
