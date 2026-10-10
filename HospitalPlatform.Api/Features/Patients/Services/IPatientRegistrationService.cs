using HospitalPlatform.Api.Features.Patients.DTOs;

namespace HospitalPlatform.Api.Features.Patients.Services;

public interface IPatientRegistrationService
{
    Task<RegisterPatientResponse> RegisterAsync(RegisterPatientRequest request);
}
