using System.ComponentModel.DataAnnotations;
using HospitalPlatform.Api.Features.Users.Validation;

namespace HospitalPlatform.Api.Features.Patients.DTOs;

public class RegisterPatientRequest : IValidatableObject
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 120 characters.")]
    public required string FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 120 characters.")]
    public required string LastName { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(12, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 12 characters.")]
    public required string Password { get; set; }

    [Required(ErrorMessage = "Identification number is required.")]
    [RegularExpression(@"\A[0-9]{8}\z", ErrorMessage = "Identification number must contain exactly eight digits, without spaces, dots or hyphens.")]
    public required string NationalIdentificationNumber { get; set; }

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Phone number must be a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    public required string PhoneNumber { get; set; }

    public required DateOnly DateOfBirth { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!EmailNormalizer.TryNormalize(Email, out _))
            yield return new ValidationResult("Email must be a valid email address.", [nameof(Email)]);

        var hospitalTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, hospitalTimeZone).DateTime);

        if (DateOfBirth == default)
            yield return new ValidationResult("Date of birth is required.", [nameof(DateOfBirth)]);
        else if (DateOfBirth > today)
            yield return new ValidationResult(
                "Date of birth cannot be in the future.",
                [nameof(DateOfBirth)]);
    }
}
