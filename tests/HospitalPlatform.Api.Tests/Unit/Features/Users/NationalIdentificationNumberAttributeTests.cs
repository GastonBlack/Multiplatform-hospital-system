using HospitalPlatform.Api.Features.Users.Validation;

namespace HospitalPlatform.Api.Tests.Unit.Features.Users;

public class NationalIdentificationNumberAttributeTests
{
    [Theory]
    [InlineData("01020304")]
    [InlineData("01234567")]
    [InlineData("00000000")]
    [InlineData("99999999")]
    public void IsValid_WithEightAsciiDigits_AcceptsWithoutChangingLeadingZeros(string value)
    {
        Assert.True(new NationalIdentificationNumberAttribute().IsValid(value));
    }

    [Theory]
    [InlineData("0123456")]
    [InlineData("012345678")]
    [InlineData("0.123.456-7")]
    [InlineData(" 01234567")]
    [InlineData("01234567 ")]
    [InlineData("01234567\n")]
    [InlineData("１２３４５６７８")]
    [InlineData("١٢٣٤٥٦٧٨")]
    [InlineData("abcdef12")]
    public void IsValid_WithFormattedOrNonAsciiIdentification_RejectsWithoutRepairing(string value)
    {
        Assert.False(new NationalIdentificationNumberAttribute().IsValid(value));
    }
}
