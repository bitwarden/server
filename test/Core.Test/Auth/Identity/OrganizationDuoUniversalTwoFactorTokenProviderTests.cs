using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Duo = DuoUniversal;

namespace Bit.Core.Test.Auth.Identity;

[SutProviderCustomize]
public class OrganizationDuoUniversalTwoFactorTokenProviderTests
{
    private readonly IDuoUniversalTokenService _duoUniversalTokenService = Substitute.For<IDuoUniversalTokenService>();
    private readonly IDataProtectorTokenFactory<DuoUserStateTokenable> _tokenDataFactory = Substitute.For<IDataProtectorTokenFactory<DuoUserStateTokenable>>();

    // Happy path
    [Theory]
    [BitAutoData]
    public async Task CanGenerateTwoFactorTokenAsync_ReturnsTrue(
        Organization organization, SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        organization.Enabled = true;
        organization.Use2fa = true;
        SetUpProperOrganizationDuoUniversalTokenService(null, organization, sutProvider);

        // Act
        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(organization);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public async Task CanGenerateTwoFactorTokenAsync_DuoTwoFactorNotEnabled_ReturnsFalse(
        Organization organization, SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProviderNotEnabledJson();
        organization.Use2fa = true;
        organization.Enabled = true;

        sutProvider.GetDependency<IDuoUniversalTokenService>()
                .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
                .Returns(true);
        // Act
        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task CanGenerateTwoFactorTokenAsync_BadMetaData_ProviderNull_ReturnsFalse(
        Organization organization, SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProviderJson();
        organization.Use2fa = true;
        organization.Enabled = true;

        sutProvider.GetDependency<IDuoUniversalTokenService>()
                .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
                .Returns(false);
        // Act
        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetDuoTwoFactorProvider_OrganizationNull_ReturnsNull(
        SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetDuoTwoFactorProvider_OrganizationNotEnabled_ReturnsNull(
        Organization organization, SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        SetUpProperOrganizationDuoUniversalTokenService(null, organization, sutProvider);
        organization.Enabled = false;

        // Act
        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(organization);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetDuoTwoFactorProvider_OrganizationUse2FAFalse_ReturnsNull(
        Organization organization, SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        SetUpProperOrganizationDuoUniversalTokenService(null, organization, sutProvider);
        organization.Use2fa = false;

        // Act
        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(organization);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetDuoClient_ProviderNull_ReturnsNull(
        SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.GenerateAsync(null, default);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetDuoClient_DuoClientNull_ReturnsNull(
        SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider,
        Organization organization)
    {
        // Arrange
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProviderJson();
        organization.Use2fa = true;
        organization.Enabled = true;

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
            .Returns(true);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .BuildDuoTwoFactorClientAsync(Arg.Any<TwoFactorProvider>())
            .Returns(null as Duo.Client);

        // Act
        var result = await sutProvider.Sut.GenerateAsync(organization, default);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateAsync_ReturnsAuthUrl(
        SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider,
        Organization organization,
        User user,
        string AuthUrl)
    {
        // Arrange
        SetUpProperOrganizationDuoUniversalTokenService(BuildDuoClient(), organization, sutProvider);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .GenerateAuthUrl(Arg.Any<Duo.Client>(), Arg.Any<IDataProtectorTokenFactory<DuoUserStateTokenable>>(), user)
            .Returns(AuthUrl);

        // Act
        var result = await sutProvider.Sut.GenerateAsync(organization, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AuthUrl, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateAsync_ClientNull_ReturnsNull(
        SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider,
        Organization organization,
        User user)
    {
        // Arrange
        SetUpProperOrganizationDuoUniversalTokenService(null, organization, sutProvider);

        // Act
        var result = await sutProvider.Sut.GenerateAsync(organization, user);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_TokenValid_ReturnsTrue(
        SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider,
        Organization organization,
        User user,
        string token)
    {
        // Arrange
        SetUpProperOrganizationDuoUniversalTokenService(BuildDuoClient(), organization, sutProvider);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .RequestDuoValidationAsync(Arg.Any<Duo.Client>(), Arg.Any<IDataProtectorTokenFactory<DuoUserStateTokenable>>(), user, token)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(token, organization, user);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ClientNull_ReturnsFalse(
    SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider,
    Organization organization,
    User user,
    string token)
    {
        // Arrange
        SetUpProperOrganizationDuoUniversalTokenService(null, organization, sutProvider);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .RequestDuoValidationAsync(Arg.Any<Duo.Client>(), Arg.Any<IDataProtectorTokenFactory<DuoUserStateTokenable>>(), user, token)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(token, organization, user);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Ensures that the IDuoUniversalTokenService is properly setup for the test.
    /// This ensures that the private GetDuoClientAsync, and GetDuoTwoFactorProvider
    /// methods will return true enabling the test to execute on the correct path.
    ///
    /// BitAutoData cannot create the Duo.Client since it does not have a public constructor
    /// so we have to use the ClientBUilder(), the client is not used meaningfully in the tests.
    /// </summary>
    /// <param name="user">user from calling test</param>
    /// <param name="sutProvider">self</param>
    private void SetUpProperOrganizationDuoUniversalTokenService(
        Duo.Client client, Organization organization, SutProvider<OrganizationDuoUniversalTokenProvider> sutProvider)
    {
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProviderJson();
        organization.Enabled = true;
        organization.Use2fa = true;

        sutProvider.GetDependency<IDuoUniversalTokenService>()
                .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
                .Returns(true);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
                .BuildDuoTwoFactorClientAsync(Arg.Any<TwoFactorProvider>())
                .Returns(client);
    }

    private Duo.Client BuildDuoClient()
    {
        var clientId = new string('c', 20);
        var clientSecret = new string('s', 40);
        return new Duo.ClientBuilder(clientId, clientSecret, "api-abcd1234.duosecurity.com", "redirectUrl").Build();
    }

    private string GetTwoFactorOrganizationDuoProviderJson()
    {
        return
            "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private string GetTwoFactorOrganizationDuoProviderNotEnabledJson()
    {
        return
            "{\"6\":{\"Enabled\":false,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }
}
