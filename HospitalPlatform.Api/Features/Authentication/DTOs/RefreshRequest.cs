using System.ComponentModel.DataAnnotations;

namespace HospitalPlatform.Api.Features.Authentication.DTOs;

public class RefreshRequest
{
    [Required(ErrorMessage = "Refresh token is required.")]
    [StringLength(64, MinimumLength = 64, ErrorMessage = "Refresh token must contain 64 characters.")]
    public required string RefreshToken { get; set; }
}
