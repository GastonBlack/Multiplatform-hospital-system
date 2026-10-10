using HospitalPlatform.Api.Features.Users.Models;

namespace HospitalPlatform.Api.Features.Patients.DTOs;

public class RegisterPatientResponse
{
    public required Guid UserId { get; set; }
    public required Guid PatientId { get; set; }
    public required AccountStatus AccountStatus { get; set; }
}
