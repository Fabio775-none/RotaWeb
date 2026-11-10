using RotaWeb.Api.Security;
using Xunit;

namespace RotaWeb.Tests;

public sealed class AdminPasswordPolicyTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Ab1!")]
    [InlineData("alllowercase1234!")]
    [InlineData("UPPERCASE123456!")]
    [InlineData("NoNumbersHere!!")]
    [InlineData("NoSymbolHere123")]
    [InlineData("AAAAaaaa1111!!")]
    public void IsValid_RejectsWeakPasswords(string value) =>
        Assert.False(AdminPasswordPolicy.IsValid(value, out _));

    [Fact]
    public void IsValid_AcceptsStrongPassword()
    {
        Assert.True(AdminPasswordPolicy.IsValid("AnOriginalPass#2026!", out var error));
        Assert.Empty(error);
    }
}
