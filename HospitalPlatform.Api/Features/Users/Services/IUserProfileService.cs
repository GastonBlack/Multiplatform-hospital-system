using HospitalPlatform.Api.Features.Users.DTOs;

namespace HospitalPlatform.Api.Features.Users.Services;

public interface IUserProfileService
{
    Task<GetUserProfile> GetProfileAsync(Guid userId);
}
