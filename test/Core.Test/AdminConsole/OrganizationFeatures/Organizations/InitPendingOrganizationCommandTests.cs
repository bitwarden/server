using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Platform.Push;
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

        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();

        await sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token);

        await organizationRepository.Received().GetByIdAsync(orgId);
        await organizationService.Received().UpdateAsync(org);
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

        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();

        await sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, collectionName, token);

        await organizationRepository.Received().GetByIdAsync(orgId);
        await organizationService.Received().UpdateAsync(org);

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

        org.Status = OrganizationStatusType.Created;

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

    private string CreateToken(OrganizationUser orgUser, Guid orgUserId, SutProvider<InitPendingOrganizationCommand> sutProvider)
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

        // Setup validator to accept the token for old InitPendingOrganizationAsync method
        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateInviteToken(orgUser, Arg.Any<User>(), protectedToken)
            .Returns(true);

        return protectedToken;
    }

    #region InitPendingOrganizationVNextAsync Tests

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_Success_WithoutCollectionName(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var requestWithoutCollection = request with { CollectionName = null };
        var updatedRequest = SetupSuccessfulInitialization(user, org, orgUser, requestWithoutCollection, sutProvider);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.False(result.IsError);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        await organizationRepository.Received(1).ExecuteOrganizationInitializationUpdatesAsync(
            Arg.Is<IEnumerable<OrganizationInitializationUpdateAction>>(list => list.Count() == 3)); // Org, OrgUser, UserEmail - no collection

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationConfirmedEmailAsync(org.DisplayName(), user.Email, orgUser.AccessSecretsManager);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushSyncOrgKeysAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_Success_WithCollectionName(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var requestWithCollection = request with { CollectionName = "My Collection" };
        var updatedRequest = SetupSuccessfulInitialization(user, org, orgUser, requestWithCollection, sutProvider);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.False(result.IsError);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        await organizationRepository.Received(1).ExecuteOrganizationInitializationUpdatesAsync(
            Arg.Is<IEnumerable<OrganizationInitializationUpdateAction>>(list => list.Count() == 4)); // Org, OrgUser, UserEmail, Collection
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_OrganizationUserNotFound_ReturnsError(
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns((OrganizationUser)null);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserNotFoundError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_InvalidToken_ReturnsError(
        User user,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        orgUser.Email = user.Email;
        var requestWithInvalidToken = request with { User = user, EmailToken = "invalid-token" };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(requestWithInvalidToken.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateInviteToken(orgUser, user, "invalid-token")
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(requestWithInvalidToken);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InvalidTokenError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_EmailMismatch_ReturnsError(
        User user,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        orgUser.Email = "different@email.com";
        user.Email = "user@email.com";

        var requestWithUser = request with { User = user, EmailToken = "valid-token" };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(requestWithUser.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateInviteToken(orgUser, user, Arg.Any<string>())
            .Returns(true);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateUserEmail(orgUser, user)
            .Returns(new EmailMismatchError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(requestWithUser);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailMismatchError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_OrganizationNotFound_ReturnsError(
        User user,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        orgUser.Email = user.Email;

        var requestWithUser = request with { User = user, EmailToken = "valid-token" };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(requestWithUser.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateInviteToken(orgUser, user, Arg.Any<string>())
            .Returns(true);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateUserEmail(orgUser, user)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(requestWithUser.OrganizationId)
            .Returns((Organization)null);

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(requestWithUser);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotFoundError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_OrganizationMismatch_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        orgUser.Email = user.Email;
        orgUser.OrganizationId = Guid.NewGuid(); // Different from request

        var requestWithUser = request with { User = user, EmailToken = "valid-token" };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(requestWithUser.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateInviteToken(orgUser, user, Arg.Any<string>())
            .Returns(true);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateUserEmail(orgUser, user)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(requestWithUser.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationMatch(orgUser, requestWithUser.OrganizationId)
            .Returns(new OrganizationMismatchError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(requestWithUser);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationMismatchError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_OrganizationAlreadyEnabled_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var updatedRequest = SetupValidOrgUserAndToken(user, orgUser, request, sutProvider);

        org.Enabled = true;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = null;
        org.PrivateKey = null;
        orgUser.OrganizationId = updatedRequest.OrganizationId;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(updatedRequest.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationMatch(orgUser, updatedRequest.OrganizationId)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationState(org)
            .Returns(new OrganizationAlreadyEnabledError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationAlreadyEnabledError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_OrganizationNotPending_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var updatedRequest = SetupValidOrgUserAndToken(user, orgUser, request, sutProvider);

        org.Enabled = false;
        org.Status = OrganizationStatusType.Created;
        org.PublicKey = null;
        org.PrivateKey = null;
        orgUser.OrganizationId = updatedRequest.OrganizationId;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(updatedRequest.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationMatch(orgUser, updatedRequest.OrganizationId)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationState(org)
            .Returns(new OrganizationNotPendingError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotPendingError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_OrganizationHasPublicKey_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var updatedRequest = SetupValidOrgUserAndToken(user, orgUser, request, sutProvider);

        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = "existing-public-key";
        org.PrivateKey = null;
        orgUser.OrganizationId = updatedRequest.OrganizationId;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(updatedRequest.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationMatch(orgUser, updatedRequest.OrganizationId)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationState(org)
            .Returns(new OrganizationHasKeysError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasKeysError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_OrganizationHasPrivateKey_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var updatedRequest = SetupValidOrgUserAndToken(user, orgUser, request, sutProvider);

        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = null;
        org.PrivateKey = "existing-private-key";
        orgUser.OrganizationId = updatedRequest.OrganizationId;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(updatedRequest.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationMatch(orgUser, updatedRequest.OrganizationId)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationState(org)
            .Returns(new OrganizationHasKeysError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasKeysError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_SingleOrgPolicyViolation_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var updatedRequest = SetupValidOrgAndOrgUser(user, org, orgUser, request, sutProvider);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidatePoliciesAsync(user, updatedRequest.OrganizationId)
            .Returns(new SingleOrgPolicyViolationError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<SingleOrgPolicyViolationError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_TwoFactorRequired_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var updatedRequest = SetupValidOrgAndOrgUser(user, org, orgUser, request, sutProvider);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidatePoliciesAsync(user, updatedRequest.OrganizationId)
            .Returns(new TwoFactorRequiredError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredError>(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_FreeOrgAdminLimit_ReturnsError(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        // Arrange
        var updatedRequest = SetupValidOrgAndOrgUser(user, org, orgUser, request, sutProvider);

        org.PlanType = PlanType.Free;
        orgUser.Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidatePoliciesAsync(user, updatedRequest.OrganizationId)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateBusinessRulesAsync(user, org, orgUser)
            .Returns(new FreeOrgAdminLimitError());

        // Act
        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(updatedRequest);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<FreeOrgAdminLimitError>(result.AsT0);
    }

    private InitPendingOrganizationRequest SetupSuccessfulInitialization(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        var updatedRequest = SetupValidOrgAndOrgUser(user, org, orgUser, request, sutProvider);

        // Setup validator to pass all policy and business rule checks
        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidatePoliciesAsync(user, updatedRequest.OrganizationId)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateBusinessRulesAsync(user, org, orgUser)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        // Setup repositories to return update delegates
        sutProvider.GetDependency<IOrganizationRepository>()
            .BuildUpdateOrganizationAction(Arg.Any<Organization>())
            .Returns(callInfo => new OrganizationInitializationUpdateAction((conn, trans, ctx) => Task.CompletedTask));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .BuildConfirmOrganizationUserAction(Arg.Any<OrganizationUser>())
            .Returns(callInfo => new OrganizationInitializationUpdateAction((conn, trans, ctx) => Task.CompletedTask));

        sutProvider.GetDependency<IUserRepository>()
            .BuildVerifyUserEmailAction(user.Id)
            .Returns(new OrganizationInitializationUpdateAction((conn, trans, ctx) => Task.CompletedTask));

        sutProvider.GetDependency<ICollectionRepository>()
            .BuildCreateDefaultCollectionAction(Arg.Any<Collection>(), Arg.Any<CollectionAccessSelection[]>())
            .Returns(callInfo => new OrganizationInitializationUpdateAction((conn, trans, ctx) => Task.CompletedTask));

        // Setup notification dependencies
        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Device>());

        return updatedRequest;
    }

    private InitPendingOrganizationRequest SetupValidOrgAndOrgUser(
        User user,
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        var updatedRequest = SetupValidOrgUserAndToken(user, orgUser, request, sutProvider);

        org.Enabled = false;
        org.Status = OrganizationStatusType.Pending;
        org.PublicKey = null;
        org.PrivateKey = null;
        orgUser.OrganizationId = updatedRequest.OrganizationId;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(updatedRequest.OrganizationId)
            .Returns(org);

        // Setup validator for organization match and state
        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationMatch(orgUser, updatedRequest.OrganizationId)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateOrganizationState(org)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        return updatedRequest;
    }

    private InitPendingOrganizationRequest SetupValidOrgUserAndToken(
        User user,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        orgUser.Email = user.Email;

        var updatedRequest = request with { User = user, EmailToken = "valid-token" };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(updatedRequest.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateInviteToken(orgUser, user, Arg.Any<string>())
            .Returns(true);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateUserEmail(orgUser, user)
            .Returns((Bit.Core.AdminConsole.Utilities.v2.Error)null);

        return updatedRequest;
    }

    #endregion
}
