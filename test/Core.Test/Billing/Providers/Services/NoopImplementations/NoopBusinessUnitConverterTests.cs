using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Providers.Services.NoopImplementations;
using Xunit;

namespace Bit.Core.Test.Billing.Providers.Services.NoopImplementations;

public class NoopBusinessUnitConverterTests
{
    private readonly NoopBusinessUnitConverter _sut = new();

    [Fact]
    public async Task FinalizeConversion_ThrowsNotSupportedException()
    {
        var organization = new Organization();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.FinalizeConversion(
                organization,
                Guid.NewGuid(),
                "token",
                "providerKey",
                "organizationKey"));
    }

    [Fact]
    public async Task InitiateConversion_ThrowsNotSupportedException()
    {
        var organization = new Organization();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.InitiateConversion(
                organization,
                "admin@example.com"));
    }

    [Fact]
    public async Task ResendConversionInvite_ThrowsNotSupportedException()
    {
        var organization = new Organization();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.ResendConversionInvite(
                organization,
                "admin@example.com"));
    }

    [Fact]
    public async Task ResetConversion_ThrowsNotSupportedException()
    {
        var organization = new Organization();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.ResetConversion(
                organization,
                "admin@example.com"));
    }

    [Fact]
    public void ImplementsIBusinessUnitConverter()
    {
        Assert.IsAssignableFrom<IBusinessUnitConverter>(_sut);
    }
}
