using System.ComponentModel.DataAnnotations;

namespace HospitalPlatform.Api.Features.Authentication.Settings;

public class JwtSettings
{
    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;
}
