using System.Security.Cryptography;
using Bit.Seeder.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Bit.SeederApi.IntegrationTest.LicenseTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Services;

/// <summary>
/// Guards <see cref="SelfHostLicenseService.WriteLicenseAsync"/>: write failures return a warning instead of throwing.
/// </summary>
public class SelfHostLicenseServiceTests
{
    [Fact]
    public async Task WriteLicenseAsync_SignerConfigured_ReportsWrittenWithoutWarning()
    {
        var licensing = new StubLicensingService((_, _) => Task.CompletedTask);
        var signer = new StubSeederLicenseSigner(_ => Task.FromResult(LicenseSigningResult.Signed("signed.jwt.token")));

        var outcome = await SelfHostLicenseService.WriteLicenseAsync(licensing, signer, NewPremiumOwner(), NullLogger.Instance);

        Assert.True(outcome.Written);
        Assert.Null(outcome.Warning);
        Assert.Single(licensing.WrittenLicenses);
    }

    [Fact]
    public async Task WriteLicenseAsync_SignerNotConfigured_ReportsSignerWarningAndWritesNothing()
    {
        var licensing = new StubLicensingService((_, _) => Task.CompletedTask);
        var signer = new StubSeederLicenseSigner(
            _ => Task.FromResult(LicenseSigningResult.Skipped("No signing certificate configured.")));

        var outcome = await SelfHostLicenseService.WriteLicenseAsync(licensing, signer, NewPremiumOwner(), NullLogger.Instance);

        Assert.False(outcome.Written);
        Assert.Equal("No signing certificate configured.", outcome.Warning);
        Assert.Empty(licensing.WrittenLicenses);
    }

    public static TheoryData<Exception> ExpectedWriteExceptions() => new()
    {
        new InvalidOperationException("boom"),
        new CryptographicException("boom"),
        new IOException("disk full"),
        new UnauthorizedAccessException("boom"),
    };

    [Theory]
    [MemberData(nameof(ExpectedWriteExceptions))]
    public async Task WriteLicenseAsync_WriteThrowsExpectedException_ReportsGenericWarningWithoutLeakingDetail(Exception thrown)
    {
        var licensing = new StubLicensingService((_, _) => throw thrown);
        var signer = new StubSeederLicenseSigner(_ => Task.FromResult(LicenseSigningResult.Signed("signed.jwt.token")));

        var outcome = await SelfHostLicenseService.WriteLicenseAsync(licensing, signer, NewPremiumOwner(), NullLogger.Instance);

        Assert.False(outcome.Written);
        Assert.False(string.IsNullOrEmpty(outcome.Warning));
        Assert.DoesNotContain(thrown.Message, outcome.Warning);
    }

    [Fact]
    public async Task WriteLicenseAsync_WriteThrowsUnexpectedException_Propagates()
    {
        var licensing = new StubLicensingService((_, _) => throw new NotSupportedException("boom"));
        var signer = new StubSeederLicenseSigner(_ => Task.FromResult(LicenseSigningResult.Signed("signed.jwt.token")));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => SelfHostLicenseService.WriteLicenseAsync(licensing, signer, NewPremiumOwner(), NullLogger.Instance));
    }
}
