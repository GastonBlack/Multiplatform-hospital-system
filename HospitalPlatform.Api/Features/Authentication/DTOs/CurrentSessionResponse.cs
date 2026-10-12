using HospitalPlatform.Api.Features.Users.Models;

namespace HospitalPlatform.Api.Features.Authentication.DTOs;

public class CurrentSessionResponse
{
    public required Guid UserId { get; set; }
    public required Guid SessionId { get; set; }
    public required ProfileType ProfileType { get; set; }
    public required AccountStatus AccountStatus { get; set; }
}
