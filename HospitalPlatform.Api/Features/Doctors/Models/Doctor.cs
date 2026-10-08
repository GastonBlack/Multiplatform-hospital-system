using HospitalPlatform.Api.Features.Users.Models;

namespace HospitalPlatform.Api.Features.Doctors.Models;

public class Doctor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required Guid UserId { get; set; }
    public ProfileType ProfileType { get; private set; } = ProfileType.Doctor;

    public required string EmployeeNumber { get; set; }
    public required string MedicalLicenseNumber { get; set; }
}
