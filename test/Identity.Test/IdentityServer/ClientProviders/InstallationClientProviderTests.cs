using Bit.Core.Auth.IdentityServer;
using Bit.Core.Platform.Installations;
using Bit.Identity.IdentityServer.ClientProviders;
using Duende.IdentityModel;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class InstallationClientProviderTests
{

    private readonly IInstallationRepository _installationRepository;
    private readonly InstallationClientProvider _sut;

    public InstallationClientProviderTests()
    {
        _installationRepository = Substitute.For<IInstallationRepository>();

        _sut = new InstallationClientProvider(_installationRepository);
    }

    [Fact]
    public async Task GetAsync_NonGuidIdentifier_ReturnsNull()
    {
        var installationClient = await _sut.GetAsync("non-guid");

        Assert.Null(installationClient);
    }

    [Fact]
    public async Task GetAsync_NonExistingInstallationGuid_ReturnsNull()
    {
        var installationClient = await _sut.GetAsync(Guid.NewGuid().ToString());

        Assert.Null(installationClient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAsync_ExistingClient_ReturnsClientRespectingEnabledStatus(bool enabled)
    {
        var installationId = Guid.NewGuid();

        _installationRepository
            .GetByIdAsync(installationId)
            .Returns(new Installation
            {
                Id = installationId,
                Key = "some-key",
                Email = "some-email",
                Enabled = enabled,
            });

        var installationClient = await _sut.GetAsync(installationId.ToString());

        Assert.NotNull(installationClient);
        Assert.Equal($"installation.{installationId}", installationClient.ClientId);
        Assert.True(installationClient.RequireClientSecret);
        // The usage of this secret is tested in integration tests
        Assert.Single(installationClient.ClientSecrets);
        Assert.Collection(
            installationClient.AllowedScopes,
            s => Assert.Equal(ApiScopes.ApiPush, s),
            s => Assert.Equal(ApiScopes.ApiLicensing, s),
            s => Assert.Equal(ApiScopes.ApiInstallation, s)
        );
        Assert.Equal(enabled, installationClient.Enabled);
        Assert.Equal(TimeSpan.FromDays(1).TotalSeconds, installationClient.AccessTokenLifetime);
        var claim = Assert.Single(installationClient.Claims);
        Assert.Equal(JwtClaimTypes.Subject, claim.Type);
        Assert.Equal(installationId.ToString(), claim.Value);
    }
}
