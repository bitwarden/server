using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
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
        Guid organizationId,
        OrganizationInviteLink existingLink,
        SutProvider<DeleteOrganizationInviteLinkCommand> sutProvider)
    {
        existingLink.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(existingLink);

        var result = await sutProvider.Sut.DeleteAsync(organizationId);

        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .Received(1)
            .DeleteAsync(existingLink);
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
    }
}
