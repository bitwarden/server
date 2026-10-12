using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class GetOrganizationInviteCommandTests
{
    [Theory, BitAutoData]
    public async Task GetInviteAsync_WhenValidationPasses_ReturnsInvite(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);

        // Act
        var result = await sutProvider.Sut.GetInviteAsync(BuildRequest(organization, inviteLink, user));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(inviteLink.Invite, result.AsSuccess);
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_WhenValidationFails_ReturnsError(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        sutProvider.GetDependency<IGetOrganizationInviteValidator>()
            .ValidateAsync(Arg.Any<OrganizationInviteLinkValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<OrganizationInviteLinkValidationRequest>(), new EmailNotVerified()));

        // Act
        var result = await sutProvider.Sut.GetInviteAsync(BuildRequest(organization, inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailNotVerified>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_PassesLookedUpLinkOrganizationAndMembershipToValidator(
        Organization organization, OrganizationInviteLink inviteLink, User user, OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        var request = BuildRequest(organization, inviteLink, user);

        // Act
        await sutProvider.Sut.GetInviteAsync(request);

        // Assert
        await sutProvider.GetDependency<IGetOrganizationInviteValidator>().Received(1)
            .ValidateAsync(Arg.Is<OrganizationInviteLinkValidationRequest>(r =>
                r.InviteLink == inviteLink &&
                r.Code == request.Code &&
                r.Organization == organization &&
                r.User == user &&
                r.ExistingOrganizationUser == existingOrganizationUser));
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_WithEmailInvitation_PassesItAsExistingMembership(
        Organization organization, OrganizationInviteLink inviteLink, User user, OrganizationUser invitedOrganizationUser,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        invitedOrganizationUser.Status = OrganizationUserStatusType.Invited;
        invitedOrganizationUser.UserId = null;
        invitedOrganizationUser.Email = user.Email;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(invitedOrganizationUser);

        // Act
        await sutProvider.Sut.GetInviteAsync(BuildRequest(organization, inviteLink, user));

        // Assert
        await sutProvider.GetDependency<IGetOrganizationInviteValidator>().Received(1)
            .ValidateAsync(Arg.Is<OrganizationInviteLinkValidationRequest>(r =>
                r.ExistingOrganizationUser == invitedOrganizationUser));
    }

    private static GetOrganizationInviteRequest BuildRequest(
        Organization organization, OrganizationInviteLink inviteLink, User user) =>
        new()
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };

    // Stubs the lookups to resolve the given link, organization, and membership, and a successful validation.
    private static void SetupHappyPath(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser? existingOrganizationUser,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = Guid.NewGuid().ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(existingOrganizationUser);

        sutProvider.GetDependency<IGetOrganizationInviteValidator>()
            .ValidateAsync(Arg.Any<OrganizationInviteLinkValidationRequest>())
            .Returns(ci => Valid(ci.Arg<OrganizationInviteLinkValidationRequest>()));
    }
}
