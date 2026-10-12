namespace HospitalPlatform.Api.Features.Authentication.DTOs;

public class AuthenticationResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required DateTimeOffset AccessTokenExpiresAt { get; set; }
    public required DateTimeOffset SessionExpiresAt { get; set; }
}
