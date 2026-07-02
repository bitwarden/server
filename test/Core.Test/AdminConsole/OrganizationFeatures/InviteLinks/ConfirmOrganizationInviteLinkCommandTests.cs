using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class ConfirmOrganizationInviteLinkCommandTests
{
    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenValidationFails_ReturnsErrorAndDoesNotWrite(
        ConfirmOrganizationInviteLinkRequest request,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IConfirmOrganizationInviteLinkValidator>()
            .ValidateAsync(Arg.Any<ConfirmOrganizationInviteLinkValidationRequest>())
            .Returns(new InviteLinkNotFound());

        var result = await sutProvider.Sut.ConfirmAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WithExistingMembership_ConfirmsDirectlyWithoutCreatingMembership(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);

        var request = BuildRequest(inviteLink, user);
        var result = await sutProvider.Sut.ConfirmAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == existingOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Confirmed &&
                ou.UserId == user.Id &&
                ou.Email == null &&
                ou.Key == request.OrgUserKey));
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WithNewUser_CreatesMembershipDirectlyInConfirmedStatus(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        organization.Seats = null;

        var request = BuildRequest(inviteLink, user);
        var result = await sutProvider.Sut.ConfirmAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationUser>(ou =>
                ou.OrganizationId == organization.Id &&
                ou.UserId == user.Id &&
                ou.Status == OrganizationUserStatusType.Confirmed &&
                ou.Type == OrganizationUserType.User &&
                ou.Key == request.OrgUserKey));
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WithNewUser_AtCapacity_AutoAddsSeat(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        organization.Seats = 2;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

        var result = await sutProvider.Sut.ConfirmAsync(BuildRequest(inviteLink, user));

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .AutoAddSeatsAsync(organization, 1);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WithNewUser_SeatExpansionFails_ReturnsErrorAndDoesNotCreate(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        organization.Seats = 2;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });
        sutProvider.GetDependency<IOrganizationService>()
            .AutoAddSeatsAsync(organization, 1)
            .ThrowsAsync(new BadRequestException("No payment method."));

        var result = await sutProvider.Sut.ConfirmAsync(BuildRequest(inviteLink, user));

        Assert.True(result.IsError);
        Assert.IsType<ConfirmSeatAddFailed>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenAutoEnrollEnabledAndKeyMissing_ReturnsResetPasswordKeyRequired(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        SetupAutoEnrollPolicy(organization, user, sutProvider);

        var request = BuildRequest(inviteLink, user) with { ResetPasswordKey = null };
        var result = await sutProvider.Sut.ConfirmAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ConfirmResetPasswordKeyRequired>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenAutoEnrollEnabledWithValidKey_EnrollsUser(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        SetupAutoEnrollPolicy(organization, user, sutProvider);

        var request = BuildRequest(inviteLink, user) with { ResetPasswordKey = "2.validresetpasswordkey" };
        var result = await sutProvider.Sut.ConfirmAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>()
            .Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(organization.Id, user.Id, request.ResetPasswordKey, user.Id);
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenDataOwnershipApplies_CreatesDefaultCollection(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        organization.UseMyItems = true;
        SetupDataOwnershipPolicy(organization, existingOrganizationUser, user, sutProvider);

        var request = BuildRequest(inviteLink, user);
        var result = await sutProvider.Sut.ConfirmAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateDefaultCollectionsAsync(
                organization.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(existingOrganizationUser.Id)),
                request.DefaultUserCollectionName);
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenDataOwnershipDoesNotApply_DoesNotCreateDefaultCollection(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // No data ownership policy details are configured, so the policy does not apply.
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);

        var result = await sutProvider.Sut.ConfirmAsync(BuildRequest(inviteLink, user));

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }


    private static ConfirmOrganizationInviteLinkRequest BuildRequest(OrganizationInviteLink inviteLink, User user) =>
        new()
        {
            Code = inviteLink.Code,
            User = user,
            OrgUserKey = "4.orgUserKey",
            DefaultUserCollectionName = "2.defaultCollectionName",
        };

    private static void SetupHappyPath(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser? existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        inviteLink.OrganizationId = organization.Id;
        if (existingOrganizationUser is not null)
        {
            existingOrganizationUser.OrganizationId = organization.Id;
            existingOrganizationUser.UserId = user.Id;
            existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        }

        sutProvider.GetDependency<IConfirmOrganizationInviteLinkValidator>()
            .ValidateAsync(Arg.Any<ConfirmOrganizationInviteLinkValidationRequest>())
            .Returns(new ConfirmOrganizationInviteLinkValidationResult
            {
                InviteLink = inviteLink,
                Organization = organization,
                ExistingOrganizationUser = existingOrganizationUser,
            });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 0, Sponsored = 0 });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<ResetPasswordPolicyRequirement>(user.Id)
            .Returns(new ResetPasswordPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(user.Id)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Disabled, []));
    }

    private static void SetupAutoEnrollPolicy(
        Organization organization,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<ResetPasswordPolicyRequirement>(user.Id)
            .Returns(new ResetPasswordPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organization.Id,
                    PolicyType = PolicyType.ResetPassword,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                    PolicyData = "{\"autoEnrollEnabled\": true}"
                }
            ]));
    }

    private static void SetupDataOwnershipPolicy(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(user.Id)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled,
            [
                new PolicyDetails
                {
                    OrganizationId = organization.Id,
                    OrganizationUserId = organizationUser.Id,
                    PolicyType = PolicyType.OrganizationDataOwnership,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                }
            ]));
    }
}
