using System.ComponentModel.DataAnnotations;

namespace HospitalPlatform.Api.Features.Authentication.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(12, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 12 characters.")]
    public required string Password { get; set; }
}
