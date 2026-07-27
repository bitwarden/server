using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Users;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Users;

[SutProviderCustomize]
public class PostUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task PostUser_Success(SutProvider<PostUserCommand> sutProvider, string externalId, Guid organizationId, List<BaseScimUserModel.EmailModel> emails, ICollection<OrganizationUserUserDetails> organizationUsers, Core.Entities.OrganizationUser newUser, Organization organization)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = externalId,
            Emails = emails,
            Active = true,
            Schemas = [ScimConstants.Scim2SchemaUser]
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IStripePaymentService>().HasSecretsManagerStandalone(organization).Returns(true);

        sutProvider.GetDependency<IOrganizationService>()
            .InviteUserAsync(organizationId,
                invitingUserId: null,
                EventSystemUser.SCIM,
                Arg.Is<OrganizationUserInvite>(i =>
                    i.Emails.Single().Equals(scimUserRequestModel.PrimaryEmail.ToLowerInvariant()) &&
                    i.Type == OrganizationUserType.User &&
                    !i.Collections.Any() &&
                    !i.Groups.Any() &&
                    i.AccessSecretsManager),
                externalId)
            .Returns(newUser);

        var user = await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).InviteUserAsync(organizationId,
            invitingUserId: null, EventSystemUser.SCIM,
            Arg.Is<OrganizationUserInvite>(i =>
                i.Emails.Single().Equals(scimUserRequestModel.PrimaryEmail.ToLowerInvariant()) &&
                i.Type == OrganizationUserType.User &&
                !i.Collections.Any() &&
                !i.Groups.Any() &&
                i.AccessSecretsManager), externalId);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetDetailsByIdAsync(newUser.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_EmptyPrimaryEmail_WithNonEmptyFallbackEmail_UsesNonEmptyEmail(
        SutProvider<PostUserCommand> sutProvider,
        string externalId,
        Guid organizationId,
        ICollection<OrganizationUserUserDetails> organizationUsers,
        Core.Entities.OrganizationUser newUser,
        Organization organization)
    {
        const string nonEmptyEmail = "user1@minimumviable.horse";
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = externalId,
            Active = true,
            Schemas = [ScimConstants.Scim2SchemaUser],
            Emails =
            [
                new BaseScimUserModel.EmailModel { Primary = true, Type = "internal", Value = "" },
                new BaseScimUserModel.EmailModel { Primary = false, Type = "external", Value = nonEmptyEmail }
            ]
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IStripePaymentService>().HasSecretsManagerStandalone(organization).Returns(true);

        sutProvider.GetDependency<IOrganizationService>()
            .InviteUserAsync(organizationId,
                invitingUserId: null,
                EventSystemUser.SCIM,
                Arg.Is<OrganizationUserInvite>(i => i.Emails.Single().Equals(nonEmptyEmail)),
                externalId)
            .Returns(newUser);

        var user = await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).InviteUserAsync(
            organizationId,
            invitingUserId: null,
            EventSystemUser.SCIM,
            Arg.Is<OrganizationUserInvite>(i => i.Emails.Single().Equals(nonEmptyEmail)),
            externalId);
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_NullEmail_Throws(SutProvider<PostUserCommand> sutProvider, Guid organizationId)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            Emails = new List<BaseScimUserModel.EmailModel>(),
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_Inactive_Throws(SutProvider<PostUserCommand> sutProvider, Guid organizationId, List<BaseScimUserModel.EmailModel> emails)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            Emails = emails,
            Active = false,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_DuplicateExternalId_Throws(SutProvider<PostUserCommand> sutProvider, Guid organizationId, List<BaseScimUserModel.EmailModel> emails, ICollection<OrganizationUserUserDetails> organizationUsers)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = organizationUsers.First().ExternalId,
            Emails = emails,
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        await Assert.ThrowsAsync<ConflictException>(async () => await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_DuplicateUserName_Throws(SutProvider<PostUserCommand> sutProvider, Guid organizationId, List<BaseScimUserModel.EmailModel> emails, ICollection<OrganizationUserUserDetails> organizationUsers)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            UserName = organizationUsers.First().ExternalId,
            Emails = emails,
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        await Assert.ThrowsAsync<ConflictException>(async () => await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_InviteUsersAfterProvisioningDisabled_StagesUser(
        SutProvider<PostUserCommand> sutProvider,
        string email,
        string externalId,
        Guid organizationId,
        ICollection<OrganizationUserUserDetails> organizationUsers,
        Core.Entities.OrganizationUser stagedUser,
        OrganizationUserUserDetails stagedUserDetails,
        Organization organization)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = externalId,
            Emails = [new BaseScimUserModel.EmailModel(email)],
            Active = true,
            Schemas = [ScimConstants.Scim2SchemaUser]
        };
        var expectedEmail = email.ToLowerInvariant();

        sutProvider.GetDependency<IScimContext>().ScimConfiguration
            .Returns(new ScimConfig { Enabled = true, InviteUsersAfterProvisioning = false });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM34423StagedStatus)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        CommandResult<ICollection<Core.Entities.OrganizationUser>> commandResult =
            new List<Core.Entities.OrganizationUser> { stagedUser };
        sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>()
            .RunAsync(Arg.Any<CreateStagedOrganizationUsersRequest>())
            .Returns(commandResult);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByIdAsync(stagedUser.Id)
            .Returns(stagedUserDetails);

        var user = await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel);

        Assert.Equal(stagedUserDetails, user);
        await sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>().Received(1)
            .RunAsync(Arg.Is<CreateStagedOrganizationUsersRequest>(r =>
                r.Organization == organization &&
                r.EventSystemUser == EventSystemUser.SCIM &&
                r.Users.Count() == 1 &&
                r.Users.Single().Email == expectedEmail &&
                r.Users.Single().ExternalId == externalId));
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs()
            .InviteUserAsync(default, default, default, default, default);
        await sutProvider.GetDependency<IInviteOrganizationUsersCommand>().DidNotReceiveWithAnyArgs()
            .InviteScimOrganizationUserAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_InviteUsersAfterProvisioningDisabled_FeatureFlagDisabled_InvitesUser(
        SutProvider<PostUserCommand> sutProvider,
        string email,
        string externalId,
        Guid organizationId,
        ICollection<OrganizationUserUserDetails> organizationUsers,
        Core.Entities.OrganizationUser newUser,
        Organization organization)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = externalId,
            Emails = [new BaseScimUserModel.EmailModel(email)],
            Active = true,
            Schemas = [ScimConstants.Scim2SchemaUser]
        };

        sutProvider.GetDependency<IScimContext>().ScimConfiguration
            .Returns(new ScimConfig { Enabled = true, InviteUsersAfterProvisioning = false });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM34423StagedStatus)
            .Returns(false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IOrganizationService>()
            .InviteUserAsync(organizationId, invitingUserId: null, EventSystemUser.SCIM,
                Arg.Any<OrganizationUserInvite>(), externalId)
            .Returns(newUser);

        await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel);

        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .InviteUserAsync(organizationId, invitingUserId: null, EventSystemUser.SCIM,
                Arg.Any<OrganizationUserInvite>(), externalId);
        await sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>().DidNotReceiveWithAnyArgs()
            .RunAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_InviteUsersAfterProvisioningEnabled_InvitesUser(
        SutProvider<PostUserCommand> sutProvider,
        string email,
        string externalId,
        Guid organizationId,
        ICollection<OrganizationUserUserDetails> organizationUsers,
        Core.Entities.OrganizationUser newUser,
        Organization organization)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = externalId,
            Emails = [new BaseScimUserModel.EmailModel(email)],
            Active = true,
            Schemas = [ScimConstants.Scim2SchemaUser]
        };

        sutProvider.GetDependency<IScimContext>().ScimConfiguration
            .Returns(new ScimConfig { Enabled = true, InviteUsersAfterProvisioning = true });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM34423StagedStatus)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IOrganizationService>()
            .InviteUserAsync(organizationId, invitingUserId: null, EventSystemUser.SCIM,
                Arg.Any<OrganizationUserInvite>(), externalId)
            .Returns(newUser);

        await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel);

        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .InviteUserAsync(organizationId, invitingUserId: null, EventSystemUser.SCIM,
                Arg.Any<OrganizationUserInvite>(), externalId);
        await sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>().DidNotReceiveWithAnyArgs()
            .RunAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_NoScimConfig_FeatureFlagEnabled_InvitesUser(
        SutProvider<PostUserCommand> sutProvider,
        string email,
        string externalId,
        Guid organizationId,
        ICollection<OrganizationUserUserDetails> organizationUsers,
        Core.Entities.OrganizationUser newUser,
        Organization organization)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = externalId,
            Emails = [new BaseScimUserModel.EmailModel(email)],
            Active = true,
            Schemas = [ScimConstants.Scim2SchemaUser]
        };

        // No SCIM configuration stored on the connection - missing InviteUsersAfterProvisioning reads as true
        sutProvider.GetDependency<IScimContext>().ScimConfiguration.Returns((ScimConfig)null);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM34423StagedStatus)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IOrganizationService>()
            .InviteUserAsync(organizationId, invitingUserId: null, EventSystemUser.SCIM,
                Arg.Any<OrganizationUserInvite>(), externalId)
            .Returns(newUser);

        await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel);

        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .InviteUserAsync(organizationId, invitingUserId: null, EventSystemUser.SCIM,
                Arg.Any<OrganizationUserInvite>(), externalId);
        await sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>().DidNotReceiveWithAnyArgs()
            .RunAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_InviteUsersAfterProvisioningDisabled_ExistingEmail_Throws(
        SutProvider<PostUserCommand> sutProvider,
        string externalId,
        Guid organizationId,
        ICollection<OrganizationUserUserDetails> organizationUsers)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = externalId,
            Emails = [new BaseScimUserModel.EmailModel(organizationUsers.First().Email)],
            Active = true,
            Schemas = [ScimConstants.Scim2SchemaUser]
        };

        sutProvider.GetDependency<IScimContext>().ScimConfiguration
            .Returns(new ScimConfig { Enabled = true, InviteUsersAfterProvisioning = false });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM34423StagedStatus)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns(organizationUsers);

        await Assert.ThrowsAsync<ConflictException>(
            async () => await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel));

        await sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>().DidNotReceiveWithAnyArgs()
            .RunAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task PostUser_InviteUsersAfterProvisioningDisabled_Inactive_Throws(
        SutProvider<PostUserCommand> sutProvider,
        string email,
        string externalId,
        Guid organizationId)
    {
        var scimUserRequestModel = new ScimUserRequestModel
        {
            ExternalId = externalId,
            Emails = [new BaseScimUserModel.EmailModel(email)],
            Active = false,
            Schemas = [ScimConstants.Scim2SchemaUser]
        };

        sutProvider.GetDependency<IScimContext>().ScimConfiguration
            .Returns(new ScimConfig { Enabled = true, InviteUsersAfterProvisioning = false });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM34423StagedStatus)
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.PostUserAsync(organizationId, scimUserRequestModel));

        await sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>().DidNotReceiveWithAnyArgs()
            .RunAsync(default);
    }
}
