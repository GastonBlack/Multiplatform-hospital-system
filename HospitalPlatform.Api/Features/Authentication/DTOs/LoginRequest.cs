using System.ComponentModel.DataAnnotations;
using HospitalPlatform.Api.Features.Users.Validation;

namespace HospitalPlatform.Api.Features.Authentication.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "Identification number is required.")]
    [NationalIdentificationNumber]
    public required string NationalIdentificationNumber { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(12, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 12 characters.")]
    public required string Password { get; set; }
}
