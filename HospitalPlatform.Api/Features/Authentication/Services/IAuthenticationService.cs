using HospitalPlatform.Api.Features.Authentication.DTOs;

namespace HospitalPlatform.Api.Features.Authentication.Services;

public interface IAuthenticationService
{
    Task<AuthenticationResponse> LoginAsync(LoginRequest request);
    Task<AuthenticationResponse> RefreshAsync(RefreshRequest request);
    Task LogoutAsync(Guid userId, Guid sessionId);
}
