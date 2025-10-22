using System.Security.Claims;
using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Identity;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Identity.IdentityServer;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Models;
using NSubstitute;
using Xunit;
using AuthFixtures = Bit.Identity.Test.AutoFixture;

namespace Bit.Identity.Test.IdentityServer;

public class ProfileServiceTests
{
    private readonly IUserService _userService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly ILicensingService _licensingService;
    private readonly ICurrentContext _currentContext;
    private readonly ProfileService _sut;

    public ProfileServiceTests()
    {
        _userService = Substitute.For<IUserService>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _providerUserRepository = Substitute.For<IProviderUserRepository>();
        _providerOrganizationRepository = Substitute.For<IProviderOrganizationRepository>();
        _licensingService = Substitute.For<ILicensingService>();
        _currentContext = Substitute.For<ICurrentContext>();

        _sut = new ProfileService(
            _userService,
            _organizationUserRepository,
            _providerUserRepository,
            _providerOrganizationRepository,
            _licensingService,
            _currentContext);
    }

    /// <summary>
    /// For Bitwarden Sends, the zero-knowledge feature architecture is enforced by preserving claims as issued,
    /// without attempting user lookup or claims mutation.
    /// When acting on behalf of a Send client, the service preserves existing claims, including those issued
    /// by the SendAccessGrantValidator, and returns without further claims lookup.
    /// </summary>
    /// <seealso cref="Bit.Identity.IdentityServer.RequestValidators.SendAccess.SendAccessGrantValidator"/>
    [Theory, BitAutoData]
    public async Task GetProfileDataAsync_SendClient_PreservesExistingClaims(
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context)
    {
        context.Client.ClientId = BitwardenClient.Send;
        var existingClaims = new[]
        {
            new Claim(Claims.SendAccessClaims.SendId, Guid.NewGuid().ToString()), new Claim("send_access", "test")
        };
        context.Subject = new ClaimsPrincipal(new ClaimsIdentity(existingClaims));

        await _sut.GetProfileDataAsync(context);

        Assert.Equal(existingClaims.Length, context.IssuedClaims.Count);
        Assert.All(existingClaims, existingClaim =>
            Assert.Contains(context.IssuedClaims, issuedClaim => issuedClaim.Type == existingClaim.Type
                                                                 && issuedClaim.Value == existingClaim.Value));
    }

    /// <summary>
    /// For Bitwarden Sends, Send access tokens neither represent a user state nor require user profile data.
    /// The SendAccessGrantValidator handles validity of requests, including resource passwords and 2FA.
    /// Separation of concerns dictates that actions on behalf of Send clients should complete without
    /// further lookup of user data.
    /// </summary>
    /// <seealso cref="Bit.Identity.IdentityServer.RequestValidators.SendAccess.SendAccessGrantValidator"/>
    [Theory, BitAutoData]
    public async Task GetProfileDataAsync_SendClient_DoesNotCallUserService(
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context)
    {
        context.Client.ClientId = BitwardenClient.Send;

        await _sut.GetProfileDataAsync(context);

        await _userService.DidNotReceive().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>());
    }

    /// <summary>
    /// For Bitwarden Sends, the client is treated as having always-active behavior, and is neither representative of
    /// a user state nor requires user profile data.
    /// </summary>
    /// <seealso cref="Bit.Identity.IdentityServer.RequestValidators.SendAccess.SendAccessGrantValidator"/>
    [Theory, BitAutoData]
    public async Task IsActiveAsync_SendClient_ReturnsTrue(
        [AuthFixtures.IsActiveContext] IsActiveContext context)
    {
        context.Client.ClientId = BitwardenClient.Send;
        context.IsActive = false;

        await _sut.IsActiveAsync(context);

        Assert.True(context.IsActive);
    }

    /// <summary>
    /// For Bitwarden Sends, the client should not interrogate the user principal as part of evaluating
    /// whether it is active.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IsActiveAsync_SendClient_DoesNotCallUserService(
        [AuthFixtures.IsActiveContext] IsActiveContext context)
    {
        context.Client.ClientId = BitwardenClient.Send;

        await _sut.IsActiveAsync(context);

        await _userService.DidNotReceive().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>());
    }

    /// <summary>
    /// When IdentityServer issues a new access token or services a UserInfo request for a given user,
    /// re-evaluate the claims for that user to ensure freshness.
    /// Organization-specific claims should be filtered out if the user is null for any reason.
    /// This allows users to continue acting on their own behalf from a valid authenticated state, but enforces
    /// a security boundary which prevents leaking of organization data and ensures organization claims,
    /// which are more likely to change than user claims, are accurate and not present if the user cannot be
    /// verified.
    /// </summary>
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task GetProfileDataAsync_UserNull_PreservesExistingNonOrgClaims(
        string client,
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context)
    {
        context.Client.ClientId = client;
        var existingClaims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", "test@example.com"),
            new Claim(Claims.OrganizationOwner, Guid.NewGuid().ToString()) // This should be filtered out
        };
        context.Subject = new ClaimsPrincipal(new ClaimsIdentity(existingClaims));
        _userService.GetUserByPrincipalAsync(context.Subject).Returns((User)null);

        await _sut.GetProfileDataAsync(context);

        // Should preserve user claims
        Assert.Contains(context.IssuedClaims, issuedClaim => issuedClaim.Type == "sub");
        Assert.Contains(context.IssuedClaims, issuedClaim => issuedClaim.Type == "email");
        // Should filter out organization-related claims
        Assert.DoesNotContain(context.IssuedClaims, issuedClaim => issuedClaim.Type.StartsWith("org"));
    }

    /// <summary>
    /// When IdentityServer issues a new access token or services a UserInfo request for a given user,
    /// re-evaluate the claims for that user to ensure freshness.
    /// New or updated claims, including premium access and organization or provider membership,
    /// should be served with the response.
    /// </summary>
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task GetProfileDataAsync_UserExists_BuildsIdentityClaims(
        string client,
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context,
        User user)
    {
        context.Client.ClientId = client;
        user.Id = Guid.Parse(context.Subject.FindFirst("sub")!.Value);
        var orgMemberships = new List<CurrentContextOrganization>
        {
            new() { Id = Guid.NewGuid(), Type = OrganizationUserType.User }
        };
        var providerMemberships = new List<CurrentContextProvider>();

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);
        _licensingService.ValidateUserPremiumAsync(user).Returns(true);
        _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id)
            .Returns(orgMemberships);
        _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id)
            .Returns(providerMemberships);

        await _sut.GetProfileDataAsync(context);

        Assert.NotEmpty(context.IssuedClaims);
        Assert.Contains(context.IssuedClaims,
            issuedClaim => issuedClaim.Type == Claims.Premium &&
                           issuedClaim.Value.Equals("true", StringComparison.CurrentCultureIgnoreCase));
        await _licensingService.Received(1).ValidateUserPremiumAsync(user);
        await _currentContext.Received(1).OrganizationMembershipAsync(_organizationUserRepository, user.Id);
        await _currentContext.Received(1).ProviderMembershipAsync(_providerUserRepository, user.Id);
    }

    /// <summary>
    /// OpenID Connect Core and JWT distinguish between string and boolean types. For spec compliance,
    /// boolean types should be served as booleans, not as strings (e.g., true, not "true"). See
    /// https://datatracker.ietf.org/doc/html/rfc7159#section-3, and
    /// https://datatracker.ietf.org/doc/html/rfc7519#section-2.
    /// For proper claims deserialization and type safety, ensure boolean values are treated as
    /// ClaimType.Boolean.
    /// </summary>
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task GetProfileDataAsync_UserExists_BooleanClaimsHaveBooleanType(
        string client,
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context,
        User user)
    {
        context.Client.ClientId = client;
        user.Id = Guid.Parse(context.Subject.FindFirst("sub").Value);

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);
        _licensingService.ValidateUserPremiumAsync(user).Returns(true);
        _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id)
            .Returns(new List<CurrentContextOrganization>());
        _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id)
            .Returns(new List<CurrentContextProvider>());

        await _sut.GetProfileDataAsync(context);

        var booleanClaims = context.IssuedClaims.Where(claim =>
            claim.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            claim.Value.Equals("false", StringComparison.OrdinalIgnoreCase));

        Assert.All(booleanClaims, claim =>
            Assert.Equal(ClaimValueTypes.Boolean, claim.ValueType));
    }

    /// <summary>
    /// When IdentityServer issues a new access token or services a UserInfo request for a given user,
    /// re-evaluate the claims for that user to ensure freshness.
    /// Organization-specific claims should never be allowed to persist, and should always be fetched fresh.
    /// </summary>
    /// <seealso cref="Bit.Core.Context.ICurrentContext.OrganizationMembershipAsync" />
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task GetProfileDataAsync_FiltersOutOrgClaimsFromExisting(
        string client,
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context,
        User user)
    {
        context.Client.ClientId = client;
        user.Id = Guid.Parse(context.Subject.FindFirst("sub").Value);

        var existingClaims = new[]
        {
            new Claim(Claims.OrganizationOwner, Guid.NewGuid().ToString()),
            new Claim(Claims.OrganizationAdmin, Guid.NewGuid().ToString()), new Claim("email", "test@example.com"),
            new Claim("name", "Test User")
        };
        context.Subject = new ClaimsPrincipal(new ClaimsIdentity(existingClaims));

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);
        _licensingService.ValidateUserPremiumAsync(user).Returns(false);
        _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id)
            .Returns(new List<CurrentContextOrganization>());
        _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id)
            .Returns(new List<CurrentContextProvider>());

        await _sut.GetProfileDataAsync(context);

        Assert.DoesNotContain(context.IssuedClaims, issuedClaim => issuedClaim.Type.StartsWith("org"));
        Assert.Contains(context.IssuedClaims, issuedClaim => issuedClaim.Type == "email");
        Assert.Contains(context.IssuedClaims, issuedClaim => issuedClaim.Type == "name");
    }

    /// <summary>
    /// When IdentityServer issues a new access token or services a UserInfo request for a given user,
    /// re-evaluate the claims for that user to ensure freshness.
    /// Existing claims should always be updated, even if their type exists in the incoming collection.
    /// </summary>
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task GetProfileDataAsync_NewClaimsOverrideExistingNonOrgClaims(
        string client,
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context,
        User user)
    {
        context.Client.ClientId = client;
        user.Id = Guid.Parse(context.Subject.FindFirst("sub").Value);
        user.Email = "new@example.com";

        var existingClaims = new[]
        {
            new Claim("sub", user.Id.ToString()), new Claim("email", "old@example.com"),
            new Claim(Claims.Premium, "false")
        };
        context.Subject = new ClaimsPrincipal(new ClaimsIdentity(existingClaims));

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);
        _licensingService.ValidateUserPremiumAsync(user).Returns(true);
        _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id)
            .Returns(new List<CurrentContextOrganization>());
        _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id)
            .Returns(new List<CurrentContextProvider>());

        await _sut.GetProfileDataAsync(context);

        // Should have new premium claim, not old one
        Assert.Contains(context.IssuedClaims,
            issuedClaim => issuedClaim.Type == Claims.Premium &&
                           issuedClaim.Value.Equals("true", StringComparison.CurrentCultureIgnoreCase));
        Assert.DoesNotContain(context.IssuedClaims,
            issuedClaim => issuedClaim.Type == Claims.Premium &&
                           issuedClaim.Value.Equals("false", StringComparison.CurrentCultureIgnoreCase));

        // Should have new email
        Assert.Contains(context.IssuedClaims,
            issuedClaim => issuedClaim.Type == "email" && issuedClaim.Value == "new@example.com");
        Assert.DoesNotContain(context.IssuedClaims,
            issuedClaim => issuedClaim.Type == "email" && issuedClaim.Value == "old@example.com");
    }

    /// <summary>
    /// Users may belong to multiple organizations. Claims should be properly scoped to each relevant organization
    /// and not cross-pollinate claims across organizations, and should be fetched fresh on each request.
    /// </summary>
    /// <seealso cref="Bit.Core.Context.ICurrentContext.OrganizationMembershipAsync" />
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task GetProfileDataAsync_WithMultipleOrganizations_IncludesOrgClaims(
        string client,
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context,
        User user)
    {
        context.Client.ClientId = client;
        user.Id = Guid.Parse(context.Subject.FindFirst("sub").Value);

        var orgId1 = Guid.NewGuid();
        var orgId2 = Guid.NewGuid();
        var orgMemberships = new List<CurrentContextOrganization>
        {
            new() { Id = orgId1, Type = OrganizationUserType.Owner },
            new() { Id = orgId2, Type = OrganizationUserType.Admin }
        };

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);
        _licensingService.ValidateUserPremiumAsync(user).Returns(false);
        _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id)
            .Returns(orgMemberships);
        _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id)
            .Returns(new List<CurrentContextProvider>());

        await _sut.GetProfileDataAsync(context);

        Assert.Contains(context.IssuedClaims,
            issuedClaim => issuedClaim.Type == Claims.OrganizationOwner && issuedClaim.Value == orgId1.ToString());
        Assert.Contains(context.IssuedClaims,
            issuedClaim => issuedClaim.Type == Claims.OrganizationAdmin && issuedClaim.Value == orgId2.ToString());
    }

    /// <summary>
    /// Users may belong to providers. Claims should be properly scoped to each relevant provider
    /// and not cross-pollinate claims across providers, and should be fetched fresh on each request.
    /// </summary>
    /// <seealso cref="Bit.Core.Context.ICurrentContext.ProviderMembershipAsync" />
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task GetProfileDataAsync_WithProviders_IncludesProviderClaims(
        string client,
        [AuthFixtures.ProfileDataRequestContext]
        ProfileDataRequestContext context,
        User user)
    {
        context.Client.ClientId = client;
        user.Id = Guid.Parse(context.Subject.FindFirst("sub").Value);

        var providerId = Guid.NewGuid();
        var providerMemberships = new List<CurrentContextProvider>
        {
            new() { Id = providerId, Type = ProviderUserType.ProviderAdmin }
        };

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);
        _licensingService.ValidateUserPremiumAsync(user).Returns(false);
        _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id)
            .Returns(new List<CurrentContextOrganization>());
        _currentContext.ProviderMembershipAsync(_providerUserRepository, user.Id)
            .Returns(providerMemberships);

        await _sut.GetProfileDataAsync(context);

        Assert.Contains(context.IssuedClaims, issuedClaim => issuedClaim.Type.StartsWith("provider"));
    }

    /// <summary>
    /// Evaluates the happy path for the core session invalidation mechanism.
    /// Critical events (e.g., password change) update the security stamp, and any subsequent request through
    /// this service should expose the stamp as invalid. A found user and matching security stamp
    /// prove out an active session.
    /// </summary>
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task IsActiveAsync_SecurityStampMatches_ReturnsTrue(
        string client,
        [AuthFixtures.IsActiveContext] IsActiveContext context,
        User user)
    {
        context.Client.ClientId = client;
        var securityStamp = "matching-security-stamp";
        user.SecurityStamp = securityStamp;

        context.Subject = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", user.Id.ToString()),
            new Claim(Claims.SecurityStamp, securityStamp)
        ]));

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);

        await _sut.IsActiveAsync(context);

        Assert.True(context.IsActive);
    }

    /// <summary>
    /// Critical events (e.g., password change) update the security stamp, and any subsequent request through
    /// this service should expose the stamp as invalid.
    /// See also examples for stamp invalidation (non-exhaustive):
    /// </summary>
    /// <seealso cref="Bit.Core.KeyManagement.UserKey.Implementations.RotateUserAccountKeysCommand.RotateUserAccountKeysAsync"/>
    /// <seealso cref="Bit.Core.Services.UserService.ChangePasswordAsync"/>
    /// <seealso cref="Bit.Core.Services.UserService.UpdatePasswordHash"/>
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task IsActiveAsync_SecurityStampDoesNotMatch_ReturnsFalse(
        string client,
        [AuthFixtures.IsActiveContext] IsActiveContext context,
        User user)
    {
        context.Client.ClientId = client;
        user.SecurityStamp = "current-security-stamp";

        context.Subject = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", user.Id.ToString()),
            new Claim(Claims.SecurityStamp, "old-security-stamp")
        ]));

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);

        await _sut.IsActiveAsync(context);

        Assert.False(context.IsActive);
    }

    /// <summary>
    /// Because security stamps are GUIDs, and database collations, etc., might treat case differently,
    /// a case-insensitive comparison is sufficient for proving the match of a security stamp.
    /// </summary>
    [Theory]
    [BitAutoData(BitwardenClient.Web, "CuRrEnT-StAmP")]
    [BitAutoData(BitwardenClient.Browser, "CuRrEnT-StAmP")]
    [BitAutoData(BitwardenClient.Cli, "CuRrEnT-StAmP")]
    [BitAutoData(BitwardenClient.Desktop, "CuRrEnT-StAmP")]
    [BitAutoData(BitwardenClient.Mobile, "CuRrEnT-StAmP")]
    [BitAutoData(BitwardenClient.DirectoryConnector, "CuRrEnT-StAmP")]
    public async Task IsActiveAsync_SecurityStampComparison_IsCaseInsensitive(
        string client,
        string claimStamp,
        [AuthFixtures.IsActiveContext] IsActiveContext context,
        User user)
    {
        context.Client.ClientId = client;
        user.SecurityStamp = "current-stamp";

        context.Subject = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", user.Id.ToString()),
            new Claim(Claims.SecurityStamp, claimStamp)
        ]));

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);

        await _sut.IsActiveAsync(context);

        Assert.True(context.IsActive);
    }

    /// <summary>
    /// Security stamps should be evaluated when present, but should not always be expected to be present.
    /// Given a successful user lookup, absent a security stamp, the session is treated as active.
    /// Only if the stamp is presented on context claims should it be validated.
    /// </summary>
    [Theory]
    [BitAutoData(BitwardenClient.Web)]
    [BitAutoData(BitwardenClient.Browser)]
    [BitAutoData(BitwardenClient.Cli)]
    [BitAutoData(BitwardenClient.Desktop)]
    [BitAutoData(BitwardenClient.Mobile)]
    [BitAutoData(BitwardenClient.DirectoryConnector)]
    public async Task IsActiveAsync_UserExistsButNoSecurityStampClaim_ReturnsTrue(
        string client,
        [AuthFixtures.IsActiveContext] IsActiveContext context,
        User user)
    {
        context.Client.ClientId = client;
        context.Subject = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email)
        ]));

        _userService.GetUserByPrincipalAsync(context.Subject).Returns(user);

        await _sut.IsActiveAsync(context);

        Assert.True(context.IsActive);
    }
}
