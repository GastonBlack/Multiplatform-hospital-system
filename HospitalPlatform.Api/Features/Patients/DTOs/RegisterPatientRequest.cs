namespace HospitalPlatform.Api.Features.Patients.DTOs;

public class RegisterPatientRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string NationalIdentificationNumber { get; set; }
    public required string PhoneNumber { get; set; }
    public required DateOnly DateOfBirth { get; set; }
}
