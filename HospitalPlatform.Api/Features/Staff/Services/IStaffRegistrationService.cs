using HospitalPlatform.Api.Features.Staff.DTOs;

namespace HospitalPlatform.Api.Features.Staff.Services;

public interface IStaffRegistrationService
{
    Task<RegisterStaffResponse> RegisterAsync(RegisterStaffRequest request);
}
