using System.Data.Common;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;
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

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(PolicyRequirementsFactory.GetDisabledSingleOrganizationRequirement());

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

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(PolicyRequirementsFactory.GetDisabledSingleOrganizationRequirement());

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

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(PolicyRequirementsFactory.GetDisabledSingleOrganizationRequirement());

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

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(PolicyRequirementsFactory.GetDisabledSingleOrganizationRequirement());

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

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(PolicyRequirementsFactory.GetDisabledSingleOrganizationRequirement());

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

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(PolicyRequirementsFactory.GetDisabledSingleOrganizationRequirement());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization already has a Private Key.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganization_WithSingleOrgPolicy_ThrowsBadRequest(
        User user, Guid orgId, Guid orgUserId, string publicKey,
        string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org,
        OrganizationUser orgUser, OrganizationUser orgUserFromAnotherOrg)
    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PrivateKey = null;
        org.PublicKey = null;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);

        // User has SingleOrg policy from another org
        orgUserFromAnotherOrg.OrganizationId = Guid.NewGuid();
        orgUserFromAnotherOrg.UserId = user.Id;
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(PolicyRequirementsFactory.GetEnabledSingleOrgDetail(orgUserFromAnotherOrg));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Contains("You may not create an organization. You belong to an organization which has a policy that prohibits you from being a member of any other organization.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganization_WithoutSingleOrgPolicy_Succeeds(
        User user, Guid orgId, Guid orgUserId, string publicKey,
        string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PrivateKey = null;
        org.PublicKey = null;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(org);

        // No SingleOrg policy
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(PolicyRequirementsFactory.GetDisabledSingleOrganizationRequirement());

        // Act
        await sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token);

        // Assert
        await sutProvider.GetDependency<IOrganizationRepository>().Received().GetByIdAsync(orgId);
        await sutProvider.GetDependency<IOrganizationService>().Received().UpdateAsync(org);
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

        return protectedToken;
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_NullOrgUser_ReturnsError(
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns((OrganizationUser?)null);

        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserNotFoundError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_NullOrg_ReturnsError(
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns((Organization?)null);

        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotFoundError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_ValidationFails_ReturnsError(
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateAsync(Arg.Any<InitPendingOrganizationValidationRequest>())
            .Returns(callInfo =>
            {
                var req = callInfo.Arg<InitPendingOrganizationValidationRequest>();
                return new ValidationResult<InitPendingOrganizationValidationRequest>(req, new InvalidTokenError());
            });

        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InvalidTokenError>(result.AsError);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .InitializeOrganizationAsync(Arg.Any<Organization>(), Arg.Any<Func<DbConnection, DbTransaction, Task>>());
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationVNextAsync_Success(
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        var requestWithCollection = request with { CollectionName = "My Collection" };
        SetupSuccessfulValidation(org, orgUser, requestWithCollection, sutProvider);

        var result = await sutProvider.Sut.InitPendingOrganizationVNextAsync(requestWithCollection);

        Assert.False(result.IsError);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .InitializeOrganizationAsync(
                Arg.Is<Organization>(o =>
                    o.Enabled == true &&
                    o.Status == OrganizationStatusType.Created &&
                    o.PublicKey == requestWithCollection.OrganizationKeys.PublicKey &&
                    o.PrivateKey == requestWithCollection.OrganizationKeys.WrappedPrivateKey),
                Arg.Any<Func<DbConnection, DbTransaction, Task>>());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .BuildConfirmOwnerAction(
                Arg.Is<OrganizationUser>(ou =>
                    ou.Status == OrganizationUserStatusType.Confirmed &&
                    ou.UserId == requestWithCollection.User.Id &&
                    ou.Key == requestWithCollection.EncryptedOrganizationSymmetricKey &&
                    ou.Email == null));

        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .CreateAsync(
                Arg.Is<Collection>(c => c.Name == "My Collection" && c.OrganizationId == requestWithCollection.OrganizationId),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(l => l == null),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(l => l.Any(i => i.Manage)));
    }

    private static void SetupSuccessfulValidation(
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateAsync(Arg.Any<InitPendingOrganizationValidationRequest>())
            .Returns(callInfo =>
            {
                var req = callInfo.Arg<InitPendingOrganizationValidationRequest>();
                return new ValidationResult<InitPendingOrganizationValidationRequest>(req, new OneOf.Types.None());
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .BuildConfirmOwnerAction(Arg.Any<OrganizationUser>())
            .Returns((_, __) => Task.CompletedTask);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(request.User.Id)
            .Returns(new List<Device>());

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrganizationConfirmationEmail)
            .Returns(true);
    }
}
