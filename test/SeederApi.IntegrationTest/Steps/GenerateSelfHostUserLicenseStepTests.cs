using Bit.Core.Entities;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Bit.SeederApi.IntegrationTest.LicenseTestHelpers;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

/// <summary>
/// Guards the pipeline self-hosted premium license step: non-premium/no-owner early-return, and a premium
/// owner triggers a license write.
/// </summary>
public class GenerateSelfHostUserLicenseStepTests
{
    [Fact]
    public async Task ExecuteAsync_PremiumOwner_SignerReturnsToken_WritesOneLicense()
    {
        var licensing = new StubLicensingService((_, _) => Task.CompletedTask);
        var signer = new StubSeederLicenseSigner(_ => Task.FromResult(LicenseSigningResult.Signed("signed.jwt.token")));
        var context = NewContext(new SeederSettings());
        context.Owner = NewPremiumOwner();

        await new GenerateSelfHostUserLicenseStep(licensing, signer, NullLogger<GenerateSelfHostUserLicenseStep>.Instance).ExecuteAsync(context);

        var written = Assert.Single(licensing.WrittenLicenses);
        Assert.True(written.Premium);
        Assert.False(string.IsNullOrWhiteSpace(written.Token));
    }

    [Fact]
    public async Task ExecuteAsync_NoOwner_WritesNothing()
    {
        var licensing = new StubLicensingService((_, _) => Task.CompletedTask);
        var signer = new StubSeederLicenseSigner(_ => Task.FromResult(LicenseSigningResult.Signed("signed.jwt.token")));
        var context = NewContext(new SeederSettings());

        await new GenerateSelfHostUserLicenseStep(licensing, signer, NullLogger<GenerateSelfHostUserLicenseStep>.Instance).ExecuteAsync(context);

        Assert.Empty(licensing.WrittenLicenses);
    }

    [Fact]
    public async Task ExecuteAsync_OwnerNotPremium_WritesNothing()
    {
        var licensing = new StubLicensingService((_, _) => Task.CompletedTask);
        var signer = new StubSeederLicenseSigner(_ => Task.FromResult(LicenseSigningResult.Signed("signed.jwt.token")));
        var context = NewContext(new SeederSettings());
        context.Owner = new User { Id = Guid.NewGuid(), Email = "free.user@example.com", Premium = false };

        await new GenerateSelfHostUserLicenseStep(licensing, signer, NullLogger<GenerateSelfHostUserLicenseStep>.Instance).ExecuteAsync(context);

        Assert.Empty(licensing.WrittenLicenses);
    }
}
