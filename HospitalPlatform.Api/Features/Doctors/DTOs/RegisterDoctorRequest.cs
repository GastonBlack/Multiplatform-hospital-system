using System.ComponentModel.DataAnnotations;
using HospitalPlatform.Api.Features.Users.Validation;

namespace HospitalPlatform.Api.Features.Doctors.DTOs;

public class RegisterDoctorRequest : IValidatableObject
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

    [Required(ErrorMessage = "Medical license number is required.")]
    public required string MedicalLicenseNumber { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!EmailNormalizer.TryNormalize(Email, out _))
            yield return new ValidationResult("Email must be a valid email address.", [nameof(Email)]);
    }
}
