using System.Data;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Identity.IdentityServer;
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
    private readonly IOrganizationDuoWebTokenProvider _organizationDuoWebTokenProvider;
    private readonly ITemporaryDuoWebV4SDKService _temporaryDuoWebV4SDKService;
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
        _organizationDuoWebTokenProvider = Substitute.For<IOrganizationDuoWebTokenProvider>();
        _temporaryDuoWebV4SDKService = Substitute.For<ITemporaryDuoWebV4SDKService>();
        _featureService = Substitute.For<IFeatureService>();
        _applicationCacheService = Substitute.For<IApplicationCacheService>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _ssoEmail2faSessionTokenable = Substitute.For<IDataProtectorTokenFactory<SsoEmail2faSessionTokenable>>();
        _currentContext = Substitute.For<ICurrentContext>();

        _sut = new TwoFactorAuthenticationValidator(
                    _userService,
                    _userManager,
                    _organizationDuoWebTokenProvider,
                    _temporaryDuoWebV4SDKService,
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
    public async void BuildTwoFactorResultAsync_OrganizationProviders_ReturnsNotNull(
        User user,
        Organization organization)
    {
        // Arrange
        organization.Use2fa = true;
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProviderJson();
        organization.Enabled = true;

        user.TwoFactorProviders = "";

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, organization);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, object>>(result);
        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("TwoFactorProviders"));
    }

    [Theory]
    [BitAutoData]
    public async void BuildTwoFactorResultAsync_IndividualProviders_ReturnsNotNull(
        User user,
        Organization organization)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorIndividualDuoProviderJson();

        // Act
        var result = await _sut.BuildTwoFactorResultAsync(user, organization);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, object>>(result);
        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("TwoFactorProviders"));
    }

    private UserManagerTestWrapper<User> SubstituteUserManager()
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

    private static string GetTwoFactorOrganizationDuoProviderJson()
    {
        return
            "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private static string GetTwoFactorIndividualDuoProviderJson()
    {
        return
            "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }
}
