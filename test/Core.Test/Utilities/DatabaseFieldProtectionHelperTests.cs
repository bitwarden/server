using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class DatabaseFieldProtectionHelperTests
{
    private static IDataProtector CreateDataProtector() =>
        new EphemeralDataProtectionProvider().CreateProtector(Constants.DatabaseFieldProtectorPurpose);

    [Fact]
    public void Protect_NullValue_ReturnsNull()
    {
        var dataProtector = CreateDataProtector();

        var result = DatabaseFieldProtectionHelper.Protect(dataProtector, null);

        Assert.Null(result);
    }

    [Fact]
    public void Protect_UnprotectedValue_ProtectsAndAddsPrefix()
    {
        var dataProtector = CreateDataProtector();

        var result = DatabaseFieldProtectionHelper.Protect(dataProtector, "plaintext-value");

        Assert.StartsWith(Constants.DatabaseFieldProtectedPrefix, result);
        Assert.Equal("plaintext-value", DatabaseFieldProtectionHelper.Unprotect(dataProtector, result));
    }

    [Fact]
    public void Protect_AlreadyGenuinelyProtectedValue_ReturnsValueUnchanged()
    {
        var dataProtector = CreateDataProtector();
        var alreadyProtected = string.Concat(
            Constants.DatabaseFieldProtectedPrefix, dataProtector.Protect("plaintext-value"));

        var result = DatabaseFieldProtectionHelper.Protect(dataProtector, alreadyProtected);

        Assert.Equal(alreadyProtected, result);
    }

    [Fact]
    public void Protect_ValueStartsWithPrefixButIsNotProtected_Throws()
    {
        var dataProtector = CreateDataProtector();

        Assert.Throws<InvalidOperationException>(
            () => DatabaseFieldProtectionHelper.Protect(dataProtector, "P|not-real-protector-output"));
    }

    [Fact]
    public void Unprotect_NullValue_ReturnsNull()
    {
        var dataProtector = CreateDataProtector();

        var result = DatabaseFieldProtectionHelper.Unprotect(dataProtector, null);

        Assert.Null(result);
    }

    [Fact]
    public void Unprotect_ValueWithoutPrefix_ReturnsValueUnchanged()
    {
        var dataProtector = CreateDataProtector();

        var result = DatabaseFieldProtectionHelper.Unprotect(dataProtector, "plaintext-value");

        Assert.Equal("plaintext-value", result);
    }

    [Fact]
    public void Unprotect_GenuinelyProtectedValue_ReturnsOriginalPlaintext()
    {
        var dataProtector = CreateDataProtector();
        var protectedValue = string.Concat(
            Constants.DatabaseFieldProtectedPrefix, dataProtector.Protect("plaintext-value"));

        var result = DatabaseFieldProtectionHelper.Unprotect(dataProtector, protectedValue);

        Assert.Equal("plaintext-value", result);
    }
}
