namespace HospitalPlatform.Api.Features.Users.DTOs;

public class GetUserProfile
{
    public required Guid UserId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string ProfileType { get; set; }
    public required string AccountStatus { get; set; }
}
