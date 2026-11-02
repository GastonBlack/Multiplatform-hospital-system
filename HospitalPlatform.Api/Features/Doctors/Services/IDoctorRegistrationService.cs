using HospitalPlatform.Api.Features.Doctors.DTOs;

namespace HospitalPlatform.Api.Features.Doctors.Services;

public interface IDoctorRegistrationService
{
    Task<RegisterDoctorResponse> RegisterAsync(RegisterDoctorRequest request);
}
