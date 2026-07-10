using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class DeleteOrganizationInviteLinkCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_WithExistingLink_DeletesAndReturnsSuccess(
        Organization organization,
        OrganizationInviteLink existingLink,
        SutProvider<DeleteOrganizationInviteLinkCommand> sutProvider)
    {
        organization.Enabled = true;
        organization.UseEvents = true;

        existingLink.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(existingLink);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var result = await sutProvider.Sut.DeleteAsync(organization.Id);

        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .Received(1)
            .DeleteAsync(existingLink);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationEventAsync(organization, EventType.Organization_InviteLinkDeleted);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WithNoExistingLink_ReturnsNotFoundError(
        Guid organizationId,
        SutProvider<DeleteOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns((OrganizationInviteLink?)null);

        var result = await sutProvider.Sut.DeleteAsync(organizationId);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default!);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationEventAsync(Arg.Any<Organization>(), Arg.Any<EventType>());
    }
}
