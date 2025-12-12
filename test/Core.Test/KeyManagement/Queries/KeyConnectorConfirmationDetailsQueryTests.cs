using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Queries;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Queries;

[SutProviderCustomize]
public class KeyConnectorConfirmationDetailsQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task Run_OrganizationNotFound_Throws(SutProvider<KeyConnectorConfirmationDetailsQuery> sutProvider,
        Guid userId, string orgSsoIdentifier)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Run(orgSsoIdentifier, userId));

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .ReceivedWithAnyArgs(0)
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task Run_OrganizationNotKeyConnector_Throws(
        SutProvider<KeyConnectorConfirmationDetailsQuery> sutProvider,
        Guid userId, string orgSsoIdentifier, Organization org)
    {
        org.Identifier = orgSsoIdentifier;
        org.UseKeyConnector = false;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(orgSsoIdentifier).Returns(org);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Run(orgSsoIdentifier, userId));

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .ReceivedWithAnyArgs(0)
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task Run_OrganizationUserNotFound_Throws(SutProvider<KeyConnectorConfirmationDetailsQuery> sutProvider,
        Guid userId, string orgSsoIdentifier
        , Organization org)
    {
        org.Identifier = orgSsoIdentifier;
        org.UseKeyConnector = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(orgSsoIdentifier).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(Task.FromResult<OrganizationUser>(null));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Run(orgSsoIdentifier, userId));

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetByOrganizationAsync(org.Id, userId);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_Success(SutProvider<KeyConnectorConfirmationDetailsQuery> sutProvider, Guid userId,
        string orgSsoIdentifier
        , Organization org, OrganizationUser orgUser)
    {
        org.Identifier = orgSsoIdentifier;
        org.UseKeyConnector = true;
        orgUser.OrganizationId = org.Id;
        orgUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(orgSsoIdentifier).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, userId)
            .Returns(orgUser);

        var result = await sutProvider.Sut.Run(orgSsoIdentifier, userId);

        Assert.Equal(org.Name, result.OrganizationName);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetByOrganizationAsync(org.Id, userId);
    }
}
