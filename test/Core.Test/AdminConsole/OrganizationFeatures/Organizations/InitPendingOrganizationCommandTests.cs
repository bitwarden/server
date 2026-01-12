using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class InitPendingOrganizationCommandTests
{

    private readonly IOrgUserInviteTokenableFactory _orgUserInviteTokenableFactory = Substitute.For<IOrgUserInviteTokenableFactory>();
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory, BitAutoData]
    public async Task Init_Organization_Success(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PrivateKey = null;
        org.PublicKey = null;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var organizationServcie = sutProvider.GetDependency<IOrganizationService>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();

        await sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token);

        await organizationRepository.Received().GetByIdAsync(orgId);
        await organizationServcie.Received().UpdateAsync(org);
        await collectionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_With_CollectionName_Success(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, string collectionName, OrganizationUser orgUser)
    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PrivateKey = null;
        org.PublicKey = null;
        org.Id = orgId;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var organizationServcie = sutProvider.GetDependency<IOrganizationService>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();

        await sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, collectionName, token);

        await organizationRepository.Received().GetByIdAsync(orgId);
        await organizationServcie.Received().UpdateAsync(org);

        await collectionRepository.Received().CreateAsync(
            Arg.Any<Collection>(),
            Arg.Is<List<CollectionAccessSelection>>(l => l == null),
            Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Is_Enabled(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)

    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.Enabled = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization is already enabled.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Is_Not_Pending(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)

    {


        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.Status = Enums.OrganizationStatusType.Created;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization is not on a Pending status.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Has_Public_Key(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)

    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PublicKey = publicKey;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization already has a Public Key.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Has_Private_Key(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)

    {

        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PublicKey = null;
        org.PrivateKey = privateKey;
        org.Enabled = false;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization already has a Private Key.", exception.Message);

    }

    public string CreateToken(OrganizationUser orgUser, Guid orgUserId, SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        });

        var orgUserInviteTokenable = _orgUserInviteTokenableFactory.CreateToken(orgUser);
        var protectedToken = _orgUserInviteTokenDataFactory.Protect(orgUserInviteTokenable);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUserId).Returns(orgUser);

        return protectedToken;
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithValidData_InitializesOrgAndConfirmsUser(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = orgId;
        var token = CreateToken(orgUser, orgUserId, sutProvider);
        org.Id = orgId;
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PrivateKey = null;
        org.PublicKey = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns(org);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(true);

        var autoConfirmReq = new AutomaticUserConfirmationPolicyRequirement(new List<PolicyDetails>());
        var twoFactorReq = new RequireTwoFactorPolicyRequirement(new List<PolicyDetails>());
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id).Returns(autoConfirmReq);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id).Returns(twoFactorReq);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", token, userKey);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(org.Enabled);
        Assert.Equal(OrganizationStatusType.Created, org.Status);
        Assert.Equal(publicKey, org.PublicKey);
        Assert.Equal(OrganizationUserStatusType.Confirmed, orgUser.Status);
        Assert.Equal(user.Id, orgUser.UserId);
        Assert.Equal(userKey, orgUser.Key);
        Assert.Null(orgUser.Email);
        await sutProvider.GetDependency<IOrganizationService>().Received().UpdateAsync(org);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received().ReplaceAsync(orgUser);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithInvalidToken_ReturnsInvalidTokenError(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, OrganizationUser orgUser)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUserId).Returns(orgUser);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", "invalid-token", userKey);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InvalidTokenError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithEnabledOrg_ReturnsOrganizationAlreadyEnabledError(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = orgId;
        var token = CreateToken(orgUser, orgUserId, sutProvider);
        org.Enabled = true;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", token, userKey);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationAlreadyEnabledError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithNonPendingOrg_ReturnsOrganizationNotPendingError(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = orgId;
        var token = CreateToken(orgUser, orgUserId, sutProvider);
        org.Enabled = false;
        org.Status = OrganizationStatusType.Created;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", token, userKey);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotPendingError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithExistingKeys_ReturnsOrganizationHasKeysError(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = orgId;
        var token = CreateToken(orgUser, orgUserId, sutProvider);
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = "existing-key";

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", token, userKey);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasKeysError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithEmailMismatch_ReturnsEmailMismatchError(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = "different@example.com";
        orgUser.OrganizationId = orgId;
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", token, userKey);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailMismatchError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithCollectionName_CreatesDefaultCollection(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        string collectionName, SutProvider<InitPendingOrganizationCommand> sutProvider,
        Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = orgId;
        var token = CreateToken(orgUser, orgUserId, sutProvider);
        org.Id = orgId;
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PrivateKey = null;
        org.PublicKey = null;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>().TwoFactorIsEnabledAsync(user).Returns(true);

        var autoConfirmReq = new AutomaticUserConfirmationPolicyRequirement(new List<PolicyDetails>());
        var twoFactorReq = new RequireTwoFactorPolicyRequirement(new List<PolicyDetails>());
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id).Returns(autoConfirmReq);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id).Returns(twoFactorReq);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, collectionName, token, userKey);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(
            Arg.Is<Collection>(c => c.Name == collectionName && c.OrganizationId == orgId),
            Arg.Is<List<CollectionAccessSelection>>(l => l == null),
            Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithTwoFactorRequired_UserDoesntHave_ReturnsTwoFactorRequiredError(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = orgId;
        var token = CreateToken(orgUser, orgUserId, sutProvider);
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PrivateKey = null;
        org.PublicKey = null;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>().TwoFactorIsEnabledAsync(user).Returns(false);

        var autoConfirmReq = new AutomaticUserConfirmationPolicyRequirement(new List<PolicyDetails>());
        var twoFactorReq = new RequireTwoFactorPolicyRequirement(
            new List<PolicyDetails> { new PolicyDetails { OrganizationId = orgId } });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id).Returns(autoConfirmReq);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id).Returns(twoFactorReq);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", token, userKey);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithUserAlreadyAdminOfFreeOrg_ReturnsFreeOrgAdminLimitError(
        User user, Guid orgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = orgId;
        orgUser.Type = OrganizationUserType.Owner;
        var token = CreateToken(orgUser, orgUserId, sutProvider);
        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PrivateKey = null;
        org.PublicKey = null;
        org.PlanType = PlanType.Free;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id).Returns(1);

        var autoConfirmReq = new AutomaticUserConfirmationPolicyRequirement(new List<PolicyDetails>());
        var twoFactorReq = new RequireTwoFactorPolicyRequirement(new List<PolicyDetails>());
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id).Returns(autoConfirmReq);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id).Returns(twoFactorReq);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", token, userKey);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<FreeOrgAdminLimitError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_WithMismatchedOrganizationId_ReturnsOrganizationMismatchError(
        User user, Guid orgId, Guid differentOrgId, Guid orgUserId, string publicKey, string privateKey, string userKey,
        SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = differentOrgId;

        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.Status = OrganizationStatusType.Pending;
        org.Enabled = false;
        org.PrivateKey = null;
        org.PublicKey = null;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(
            user, orgId, orgUserId, publicKey, privateKey, "", token, userKey);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationMismatchError>(result.AsError);
    }
}
