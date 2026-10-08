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
public class AssignAccessConnectorToTargetCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task AssignAsync_AccessConnectorMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid accessConnectorId, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns((PamAccessConnector?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.AssignAsync(organizationId, actingUserId, accessConnectorId, targetSystemId));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_AccessConnectorWrongOrg_ThrowsNotFound(
        Guid actingUserId, PamAccessConnector accessConnector, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.AssignAsync(Guid.NewGuid(), actingUserId, accessConnector.Id, targetSystemId));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_TargetMissing_ThrowsNotFound(
        Guid actingUserId, PamAccessConnector accessConnector, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId)
            .Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.AssignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, targetSystemId));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_TargetWrongOrg_ThrowsNotFound(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        // target.OrganizationId is not the caller's route org either.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.AssignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_AccessConnectorDisabled_ThrowsBadRequest(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Disabled;
        target.OrganizationId = accessConnector.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_TargetManual_ThrowsBadRequest(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        target.OrganizationId = accessConnector.OrganizationId;
        target.Method = PamTargetSystemMethod.Manual;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_DuplicateAssignment_ThrowsBadRequest(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        target.OrganizationId = accessConnector.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().AssignmentExistsAsync(accessConnector.Id, target.Id)
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_HappyPath_CreatesAssignment(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        target.OrganizationId = accessConnector.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().AssignmentExistsAsync(accessConnector.Id, target.Id)
            .Returns(false);

        await sutProvider.Sut.AssignAsync(accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id);

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().Received(1).CreateAssignmentAsync(Arg.Is<PamAccessConnectorTargetAssignment>(a =>
            a.AccessConnectorId == accessConnector.Id && a.TargetSystemId == target.Id
            && a.OrganizationId == accessConnector.OrganizationId
            && a.Id != Guid.Empty && a.CreationDate == _now));
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_HappyPath_EmitsAttemptThenOutcome(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        target.OrganizationId = accessConnector.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamAccessConnectorRepository>().AssignmentExistsAsync(accessConnector.Id, target.Id)
            .Returns(false);

        await sutProvider.Sut.AssignAsync(accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.AccessConnectorAssignedToTarget && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessConnectorId == accessConnector.Id && e.TargetSystemId == target.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.AccessConnectorAssignedToTarget && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessConnectorId == accessConnector.Id && e.TargetSystemId == target.Id));
    }

    private static SutProvider<AssignAccessConnectorToTargetCommand> Setup()
    {
        var sutProvider = new SutProvider<AssignAccessConnectorToTargetCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
