using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace HospitalPlatform.Api.Features.Users.Seeds;

public static class UserSeed
{
    public static User ConfigureUser(HospitalDbContext db, IPasswordHasher<User> passwordHasher, string password)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            FirstName = "Default",
            LastName = "Administrator",
            Email = "administrator0101@gmail.com",
            PasswordHash = string.Empty,
            ProfileType = ProfileType.Staff,
            AccountStatus = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        return user;
    }
}
