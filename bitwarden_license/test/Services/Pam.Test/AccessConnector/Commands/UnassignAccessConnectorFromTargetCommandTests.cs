using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Commands;

[SutProviderCustomize]
public class UnassignAccessConnectorFromTargetCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UnassignAsync_AccessConnectorMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid accessConnectorId, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns((PamAccessConnector?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(organizationId, actingUserId, accessConnectorId, targetSystemId));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_AccessConnectorWrongOrg_ThrowsNotFound(Guid actingUserId, PamAccessConnector accessConnector, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(Guid.NewGuid(), actingUserId, accessConnector.Id, targetSystemId));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_TargetMissing_ThrowsNotFound(Guid actingUserId, PamAccessConnector accessConnector, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId)
            .Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, targetSystemId));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_TargetWrongOrg_ThrowsNotFound(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_AssignmentMissing_ThrowsNotFound(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.OrganizationId = accessConnector.OrganizationId;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().AssignmentExistsAsync(accessConnector.Id, target.Id)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UnassignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAssignmentAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_HappyPath_DeletesAssignment(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.OrganizationId = accessConnector.OrganizationId;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().AssignmentExistsAsync(accessConnector.Id, target.Id)
            .Returns(true);

        await sutProvider.Sut.UnassignAsync(
            accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id);

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .Received(1).DeleteAssignmentAsync(accessConnector.Id, target.Id);
    }

    [Theory, BitAutoData]
    public async Task UnassignAsync_HappyPath_EmitsAttemptThenOutcome(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.OrganizationId = accessConnector.OrganizationId;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().AssignmentExistsAsync(accessConnector.Id, target.Id)
            .Returns(true);

        await sutProvider.Sut.UnassignAsync(
            accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.AccessConnectorUnassignedFromTarget && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessConnectorId == accessConnector.Id && e.TargetSystemId == target.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.AccessConnectorUnassignedFromTarget && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessConnectorId == accessConnector.Id && e.TargetSystemId == target.Id));
    }

    private static SutProvider<UnassignAccessConnectorFromTargetCommand> Setup()
    {
        var sutProvider = new SutProvider<UnassignAccessConnectorFromTargetCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
