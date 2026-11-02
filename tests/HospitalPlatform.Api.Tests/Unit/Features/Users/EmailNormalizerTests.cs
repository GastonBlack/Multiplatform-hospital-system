using System.Globalization;
using HospitalPlatform.Api.Features.Users.Validation;

namespace HospitalPlatform.Api.Tests.Unit.Features.Users;

public class EmailNormalizerTests
{
    [Theory]
    [InlineData(" Ana+turnos@Example.COM ", "ana+turnos@example.com")]
    [InlineData("\tA.Na+tag@Example.com\r\n", "a.na+tag@example.com")]
    [InlineData("user@example.com", "user@example.com")]
    public void TryNormalize_WithValidAddress_ReturnsCanonicalEmail(string input, string expected)
    {
        Assert.True(EmailNormalizer.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("ana perez@example.com")]
    [InlineData("ana\tperez@example.com")]
    [InlineData("ana@example.\ncom")]
    [InlineData("invalid")]
    [InlineData("ana@@example.com")]
    public void TryNormalize_WithInvalidAddress_ReturnsFalseWithoutCanonicalEmail(string? input)
    {
        Assert.False(EmailNormalizer.TryNormalize(input, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalize_UsesInvariantCasingRegardlessOfCurrentCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(EmailNormalizer.TryNormalize(" ILKER@EXAMPLE.COM ", out var normalized));
            Assert.Equal("ilker@example.com", normalized);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
