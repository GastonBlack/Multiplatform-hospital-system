using HospitalPlatform.Api.Features.Users.DTOs;
using HospitalPlatform.Api.Infrastructure.Middleware.Exceptions;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HospitalPlatform.Api.Features.Users.Services;

public class UserProfileService : IUserProfileService
{
    private readonly HospitalDbContext _db;

    public UserProfileService(HospitalDbContext db)
    {
        _db = db;
    }

    public async Task<GetUserProfile> GetProfileAsync(Guid userId)
    {
        var profile = await _db.Users.Where(user => user.Id == userId)
            .Select(user => new GetUserProfile
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                ProfileType = user.ProfileType.ToString(),
                AccountStatus = user.AccountStatus.ToString()
            }).SingleOrDefaultAsync();

        return profile ?? throw new NotFoundException("User not found.");
    }
}
