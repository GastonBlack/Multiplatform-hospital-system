using System.ComponentModel.DataAnnotations;

namespace HospitalPlatform.Api.Features.Users.Validation;

public static class EmailNormalizer
{
    public static bool TryNormalize(string? email, out string normalizedEmail)
    {
        normalizedEmail = string.Empty;

        if (string.IsNullOrWhiteSpace(email))
            return false;

        var candidate = email.Trim().ToLowerInvariant();

        if (candidate.Any(char.IsWhiteSpace) || !new EmailAddressAttribute().IsValid(candidate))
            return false;

        normalizedEmail = candidate;
        return true;
    }
}
