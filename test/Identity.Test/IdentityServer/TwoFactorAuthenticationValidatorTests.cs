using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Identity.Test.Wrappers;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using AuthFixtures = Bit.Identity.Test.AutoFixture;

namespace Bit.Identity.Test.IdentityServer;

public class TwoFactorAuthenticationValidatorTests
{
    private readonly IUserService _userService;
    private readonly UserManagerTestWrapper<User> _userManager;
    private readonly IOrganizationDuoUniversalTokenProvider _organizationDuoUniversalTokenProvider;
    private readonly IFeatureService _featureService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> _ssoEmail2faSessionTokenable;
    private readonly ICurrentContext _currentContext;
    private readonly TwoFactorAuthenticationValidator _sut;

    public TwoFactorAuthenticationValidatorTests()
    {
        _userService = Substitute.For<IUserService>();
        _userManager = SubstituteUserManager();
        _organizationDuoUniversalTokenProvider = Substitute.For<IOrganizationDuoUniversalTokenProvider>();
        _featureService = Substitute.For<IFeatureService>();
        _applicationCacheService = Substitute.For<IApplicationCacheService>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _ssoEmail2faSessionTokenable = Substitute.For<IDataProtectorTokenFactory<SsoEmail2faSessionTokenable>>();
        _currentContext = Substitute.For<ICurrentContext>();

        _sut = new TwoFactorAuthenticationValidator(
                    _userService,
                    _userManager,
                    _organizationDuoUniversalTokenProvider,
                    _featureService,
                    _applicationCacheService,
                    _organizationUserRepository,
                    _organizationRepository,
                    _ssoEmail2faSessionTokenable,
                    _currentContext);
    }

    [Theory]
    [BitAutoData("password")]
    [BitAutoData("authorization_code")]
    public async void RequiresTwoFactorAsync_IndividualOnly_Required_ReturnTrue(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request.GrantType = grantType;
        // All three of these must be true for the two factor authentication to be required
        _userManager.TWO_FACTOR_ENABLED = true;
        _userManager.SUPPORTS_TWO_FACTOR = true;
        // In order for the two factor authentication to be required, the user must have at least one two factor provider
        _userManager.TWO_FACTOR_PROVIDERS = ["email"];

        // Act
        var result = await _sut.RequiresTwoFactorAsync(user, request);

        // Assert
        Assert.True(result.Item1);
        Assert.Null(result.Item2);
    }

    [Theory]
    [BitAutoData("client_credentials")]
    [BitAutoData("webauthn")]
    public async void RequiresTwoFactorAsync_NotRequired_ReturnFalse(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request.GrantType = grantType;

        // Act
        var result = await _sut.RequiresTwoFactorAsync(user, request);

        // Assert
        Assert.False(result.Item1);
        Assert.Null(result.Item2);
    }

    [Theory]
    [BitAutoData("password")]
    [BitAutoData("authorization_code")]
    public async void RequiresTwoFactorAsync_IndividualFalse_OrganizationRequired_ReturnTrue(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user,
        OrganizationUserOrganizationDetails orgUser,
        Organization organization,
        ICollection<CurrentContextOrganization> organizationCollection)
    {
        // Arrange
        request.GrantType = grantType;
        // Link the orgUser to the User making the request
        orgUser.UserId = user.Id;
        // Link organization to the organization user
        organization.Id = orgUser.OrganizationId;

        // Set Organization 2FA to required
        organization.Use2fa = true;
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProviderJson();
        organization.Enabled = true;

        // Make sure organization list is not empty
        organizationCollection.Clear();
        // Fix OrganizationUser Permissions field
        orgUser.Permissions = "{}";
        organizationCollection.Add(new CurrentContextOrganization(orgUser));

        _currentContext.OrganizationMembershipAsync(Arg.Any<IOrganizationUserRepository>(), Arg.Any<Guid>())
            .Returns(Task.FromResult(organizationCollection));

        _applicationCacheService.GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>()
            {
                { orgUser.OrganizationId, new OrganizationAbility(organization)}
            });

        _organizationRepository.GetManyByUserIdAsync(Arg.Any<Guid>()).Returns([organization]);

        // Act
        var result = await _sut.RequiresTwoFactorAsync(user, request);

        // Assert
        Assert.True(result.Item1);
        Assert.NotNull(result.Item2);
        Assert.IsType<Organization>(result.Item2);
    }

    [Theory]
    [BitAutoData]
    public async void BuildTwoFactorResultAsync_NoProviders_ReturnsNull(
        User user,
        Organization organization)
    {
        // Arrange
        organization.Use2fa = true;
        organization.TwoFactorProviders = "{}";
        organization.Enabled = true;

        user.TwoFactorProviders = "";

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, organization);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async void BuildTwoFactorResultAsync_OrganizationProviders_NotEnabled_ReturnsNull(
        User user,
        Organization organization)
    {
        // Arrange
        organization.Use2fa = true;
        organization.TwoFactorProviders = GetTwoFactorOrganizationNotEnabledDuoProviderJson();
        organization.Enabled = true;

        user.TwoFactorProviders = null;

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, organization);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async void BuildTwoFactorResultAsync_OrganizationProviders_ReturnsNotNull(
        User user,
        Organization organization)
    {
        // Arrange
        organization.Use2fa = true;
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProviderJson();
        organization.Enabled = true;

        user.TwoFactorProviders = null;

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, organization);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, object>>(result);
        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("TwoFactorProviders2"));
    }

    [Theory]
    [BitAutoData]
    public async void BuildTwoFactorResultAsync_IndividualProviders_NotEnabled_ReturnsNull(
        User user)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorIndividualNotEnabledProviderJson(TwoFactorProviderType.Email);

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, null);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async void BuildTwoFactorResultAsync_IndividualProviders_ReturnsNotNull(
        User user)
    {
        // Arrange
        _userService.CanAccessPremium(user).Returns(true);

        user.TwoFactorProviders = GetTwoFactorIndividualProviderJson(TwoFactorProviderType.Duo);

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, null);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, object>>(result);
        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("TwoFactorProviders2"));
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Email)]
    public async void BuildTwoFactorResultAsync_IndividualEmailProvider_SendsEmail_SetsSsoToken_ReturnsNotNull(
        TwoFactorProviderType providerType,
        User user)
    {
        // Arrange
        var providerTypeInt = (int)providerType;
        user.TwoFactorProviders = GetTwoFactorIndividualProviderJson(providerType);

        _userManager.TWO_FACTOR_ENABLED = true;
        _userManager.SUPPORTS_TWO_FACTOR = true;
        _userManager.TWO_FACTOR_PROVIDERS = [providerType.ToString()];

        _userService.TwoFactorProviderIsEnabledAsync(Arg.Any<TwoFactorProviderType>(), user)
            .Returns(true);

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, null);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, object>>(result);
        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("TwoFactorProviders2"));
        var providers = (Dictionary<string, Dictionary<string, object>>)result["TwoFactorProviders2"];
        Assert.True(providers.ContainsKey(providerTypeInt.ToString()));
        Assert.True(result.ContainsKey("SsoEmail2faSessionToken"));
        Assert.True(result.ContainsKey("Email"));

        await _userService.Received(1).SendTwoFactorEmailAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.WebAuthn)]
    [BitAutoData(TwoFactorProviderType.Email)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    [BitAutoData(TwoFactorProviderType.OrganizationDuo)]
    public async void BuildTwoFactorResultAsync_IndividualProvider_ReturnMatchesType(
        TwoFactorProviderType providerType,
        User user)
    {
        // Arrange
        var providerTypeInt = (int)providerType;
        user.TwoFactorProviders = GetTwoFactorIndividualProviderJson(providerType);

        _userManager.TWO_FACTOR_ENABLED = true;
        _userManager.SUPPORTS_TWO_FACTOR = true;
        _userManager.TWO_FACTOR_PROVIDERS = [providerType.ToString()];
        _userManager.TWO_FACTOR_TOKEN = "{\"Key1\":\"WebauthnToken\"}";

        _userService.CanAccessPremium(user).Returns(true);

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, null);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, object>>(result);
        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("TwoFactorProviders2"));
        var providers = (Dictionary<string, Dictionary<string, object>>)result["TwoFactorProviders2"];
        Assert.True(providers.ContainsKey(providerTypeInt.ToString()));
    }

    [Theory]
    [BitAutoData]
    public async void VerifyTwoFactorAsync_Individual_TypeNull_ReturnsFalse(
        User user,
        string token)
    {
        // Arrange
        _userService.TwoFactorProviderIsEnabledAsync(
            TwoFactorProviderType.Email, user).Returns(true);

        _userManager.TWO_FACTOR_PROVIDERS = ["email"];

        // Act
        var result = await _sut.VerifyTwoFactor(
            user, null, TwoFactorProviderType.U2f, token);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async void VerifyTwoFactorAsync_Individual_NotEnabled_ReturnsFalse(
        User user,
        string token)
    {
        // Arrange
        _userService.TwoFactorProviderIsEnabledAsync(
            TwoFactorProviderType.Email, user).Returns(false);

        _userManager.TWO_FACTOR_PROVIDERS = ["email"];

        // Act
        var result = await _sut.VerifyTwoFactor(
            user, null, TwoFactorProviderType.Email, token);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async void VerifyTwoFactorAsync_Organization_NotEnabled_ReturnsFalse(
        User user,
        string token)
    {
        // Arrange
        _userService.TwoFactorProviderIsEnabledAsync(
            TwoFactorProviderType.OrganizationDuo, user).Returns(false);

        _userManager.TWO_FACTOR_PROVIDERS = ["OrganizationDuo"];

        // Act
        var result = await _sut.VerifyTwoFactor(
            user, null, TwoFactorProviderType.OrganizationDuo, token);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.WebAuthn)]
    [BitAutoData(TwoFactorProviderType.Email)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    [BitAutoData(TwoFactorProviderType.Remember)]
    public async void VerifyTwoFactorAsync_Individual_ValidToken_ReturnsTrue(
        TwoFactorProviderType providerType,
        User user,
        string token)
    {
        // Arrange
        _userService.TwoFactorProviderIsEnabledAsync(
            providerType, user).Returns(true);

        _userManager.TWO_FACTOR_ENABLED = true;
        _userManager.TWO_FACTOR_TOKEN_VERIFIED = true;

        // Act
        var result = await _sut.VerifyTwoFactor(user, null, providerType, token);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.WebAuthn)]
    [BitAutoData(TwoFactorProviderType.Email)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    [BitAutoData(TwoFactorProviderType.Remember)]
    public async void VerifyTwoFactorAsync_Individual_InvalidToken_ReturnsFalse(
        TwoFactorProviderType providerType,
        User user,
        string token)
    {
        // Arrange
        _userService.TwoFactorProviderIsEnabledAsync(
            providerType, user).Returns(true);

        _userManager.TWO_FACTOR_ENABLED = true;
        _userManager.TWO_FACTOR_TOKEN_VERIFIED = false;

        // Act
        var result = await _sut.VerifyTwoFactor(user, null, providerType, token);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.OrganizationDuo)]
    public async void VerifyTwoFactorAsync_Organization_ValidToken_ReturnsTrue(
        TwoFactorProviderType providerType,
        User user,
        Organization organization,
        string token)
    {
        // Arrange
        _organizationDuoUniversalTokenProvider.ValidateAsync(
            token, organization, user).Returns(true);

        _userManager.TWO_FACTOR_ENABLED = true;
        _userManager.TWO_FACTOR_TOKEN_VERIFIED = true;

        organization.Use2fa = true;
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProviderJson();
        organization.Enabled = true;

        // Act
        var result = await _sut.VerifyTwoFactor(
            user, organization, providerType, token);

        // Assert
        Assert.True(result);
    }

    private static UserManagerTestWrapper<User> SubstituteUserManager()
    {
        return new UserManagerTestWrapper<User>(
            Substitute.For<IUserTwoFactorStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
    }

    private static string GetTwoFactorOrganizationDuoProviderJson(bool enabled = true)
    {
        return
            "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private static string GetTwoFactorOrganizationNotEnabledDuoProviderJson(bool enabled = true)
    {
        return
            "{\"6\":{\"Enabled\":false,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private static string GetTwoFactorIndividualProviderJson(TwoFactorProviderType providerType)
    {
        return providerType switch
        {
            TwoFactorProviderType.Duo => "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}",
            TwoFactorProviderType.Email => "{\"1\":{\"Enabled\":true,\"MetaData\":{\"Email\":\"user@test.dev\"}}}",
            TwoFactorProviderType.WebAuthn => "{\"7\":{\"Enabled\":true,\"MetaData\":{\"Key1\":{\"Name\":\"key1\",\"Descriptor\":{\"Type\":0,\"Id\":\"keyId\",\"Transports\":null},\"PublicKey\":\"key\",\"UserHandle\":\"handle\",\"SignatureCounter\":0,\"CredType\":\"none\",\"RegDate\":\"2022-01-01T00:00:00Z\",\"AaGuid\":\"00000000-0000-0000-0000-000000000000\",\"Migrated\":false}}}}",
            TwoFactorProviderType.YubiKey => "{\"3\":{\"Enabled\":true,\"MetaData\":{\"Id\":\"yubikeyId\",\"Nfc\":true}}}",
            TwoFactorProviderType.OrganizationDuo => "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}",
            _ => "{}",
        };
    }

    private static string GetTwoFactorIndividualNotEnabledProviderJson(TwoFactorProviderType providerType)
    {
        return providerType switch
        {
            TwoFactorProviderType.Duo => "{\"2\":{\"Enabled\":false,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}",
            TwoFactorProviderType.Email => "{\"1\":{\"Enabled\":false,\"MetaData\":{\"Email\":\"user@test.dev\"}}}",
            TwoFactorProviderType.WebAuthn => "{\"7\":{\"Enabled\":false,\"MetaData\":{\"Key1\":{\"Name\":\"key1\",\"Descriptor\":{\"Type\":0,\"Id\":\"keyId\",\"Transports\":null},\"PublicKey\":\"key\",\"UserHandle\":\"handle\",\"SignatureCounter\":0,\"CredType\":\"none\",\"RegDate\":\"2022-01-01T00:00:00Z\",\"AaGuid\":\"00000000-0000-0000-0000-000000000000\",\"Migrated\":false}}}}",
            TwoFactorProviderType.YubiKey => "{\"3\":{\"Enabled\":false,\"MetaData\":{\"Id\":\"yubikeyId\",\"Nfc\":true}}}",
            TwoFactorProviderType.OrganizationDuo => "{\"6\":{\"Enabled\":false,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}",
            _ => "{}",
        };
    }
}
