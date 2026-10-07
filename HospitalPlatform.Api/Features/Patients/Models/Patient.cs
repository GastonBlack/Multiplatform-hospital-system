namespace HospitalPlatform.Api.Features.Patients.Models;

public class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required Guid UserId { get; set; }

    public required string NationalIdentificationNumber { get; set; }
    public required string PhoneNumber { get; set; }
    public required DateOnly DateOfBirth { get; set; }

    public DateTimeOffset? IdentityVerifiedAt { get; set; }
    public Guid? IdentityVerifiedByUserId { get; set; }
}
