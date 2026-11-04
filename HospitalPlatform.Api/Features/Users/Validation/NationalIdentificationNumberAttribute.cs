using System.ComponentModel.DataAnnotations;

namespace HospitalPlatform.Api.Features.Users.Validation;

public sealed class NationalIdentificationNumberAttribute : RegularExpressionAttribute
{
    public NationalIdentificationNumberAttribute() : base(@"\A[0-9]{8}\z")
    {
        ErrorMessage = "Identification number must contain exactly eight digits, without spaces, dots or hyphens.";
    }
}
