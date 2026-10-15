using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Api.Endpoints.Handlers;

/// <summary>
/// An access connector receives every cipher its target systems rotate, so assigning one needs edit access to each.
/// </summary>
[SutProviderCustomize]
public class AccessConnectorEndpointsHandlerTests
{
    [Theory, BitAutoData]
    public async Task AssignTarget_TargetRotatesACipherNotEditable_ThrowsBadRequest(
        Guid organizationId, Guid accessConnectorId, PamRotationConfigDetails config,
        SutProvider<AccessConnectorEndpointsHandler> sutProvider)
    {
        var model = new AssignAccessConnectorTargetRequestModel { TargetSystemId = config.TargetSystemId };
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetManyDetailsByOrganizationIdAsync(organizationId)
            .Returns(new List<PamRotationConfigDetails> { config });
        sutProvider.GetDependency<ICanEditRotationCiphersQuery>()
            .CanEditAllAsync(organizationId, Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { config.CipherId })))
            .Returns(false);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignTarget(organizationId, accessConnectorId, model));

        Assert.Empty(sutProvider.GetDependency<IAssignAccessConnectorToTargetCommand>().ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task AssignTarget_ChecksOnlyTheTargetsCiphers_AndAssigns(
        Guid organizationId, Guid accessConnectorId, Guid userId, PamRotationConfigDetails onTarget,
        PamRotationConfigDetails onAnotherTarget, SutProvider<AccessConnectorEndpointsHandler> sutProvider)
    {
        var model = new AssignAccessConnectorTargetRequestModel { TargetSystemId = onTarget.TargetSystemId };
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetManyDetailsByOrganizationIdAsync(organizationId)
            .Returns(new List<PamRotationConfigDetails> { onTarget, onAnotherTarget });
        sutProvider.GetDependency<ICanEditRotationCiphersQuery>()
            .CanEditAllAsync(organizationId, Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { onTarget.CipherId })))
            .Returns(true);

        await sutProvider.Sut.AssignTarget(organizationId, accessConnectorId, model);

        await sutProvider.GetDependency<IAssignAccessConnectorToTargetCommand>().Received(1)
            .AssignAsync(organizationId, userId, accessConnectorId, onTarget.TargetSystemId);
    }
}
