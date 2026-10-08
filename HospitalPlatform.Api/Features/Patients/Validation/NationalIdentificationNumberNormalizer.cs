using System.Text;

namespace HospitalPlatform.Api.Features.Patients.Validation;

public static class NationalIdentificationNumberNormalizer
{
    public static bool TryNormalize(string? identificationNumber, out string normalizedIdentificationNumber)
    {
        normalizedIdentificationNumber = string.Empty;

        if (string.IsNullOrWhiteSpace(identificationNumber))
            return false;

        var digits = new StringBuilder(8);

        foreach (var character in identificationNumber)
        {
            if (char.IsWhiteSpace(character) || character is '.' or '-')
                continue;

            if (character is < '0' or > '9' || digits.Length == 8)
                return false;

            digits.Append(character);
        }

        if (digits.Length != 8)
            return false;

        normalizedIdentificationNumber = digits.ToString();
        return true;
    }
}
