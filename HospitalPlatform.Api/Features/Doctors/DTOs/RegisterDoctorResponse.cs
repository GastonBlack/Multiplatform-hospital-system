namespace HospitalPlatform.Api.Features.Doctors.DTOs;

public class RegisterDoctorResponse
{
    public required Guid UserId { get; set; }
    public required Guid DoctorId { get; set; }
    public required string EmployeeNumber { get; set; }
    public required string MedicalLicenseNumber { get; set; }
    public required string AccountStatus { get; set; }
}
