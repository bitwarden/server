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
public class SetAccessConnectorStatusCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task SetStatusAsync_AccessConnectorMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid accessConnectorId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns((PamAccessConnector?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SetStatusAsync(organizationId, actingUserId, accessConnectorId, enable: false));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        // A cross-org id is NotFound, so it does not reveal that the connector exists.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SetStatusAsync(Guid.NewGuid(), actingUserId, accessConnector.Id, enable: false));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Disable_AlreadyDisabled_ThrowsBadRequest(
        Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Disabled;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetStatusAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, enable: false));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Enable_AlreadyEnabled_ThrowsBadRequest(
        Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetStatusAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, enable: true));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Disable_SetsDisabled(Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await sutProvider.Sut.SetStatusAsync(
            accessConnector.OrganizationId, actingUserId, accessConnector.Id, enable: false);

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .Received(1).ReplaceAsync(Arg.Is<PamAccessConnector>(d =>
            d.Id == accessConnector.Id && d.Status == PamAccessConnectorStatus.Disabled && d.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Enable_SetsEnabled(Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Disabled;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await sutProvider.Sut.SetStatusAsync(
            accessConnector.OrganizationId, actingUserId, accessConnector.Id, enable: true);

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .Received(1).ReplaceAsync(Arg.Is<PamAccessConnector>(d =>
            d.Id == accessConnector.Id && d.Status == PamAccessConnectorStatus.Enabled && d.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Disable_EmitsAttemptThenOutcome(
        Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await sutProvider.Sut.SetStatusAsync(
            accessConnector.OrganizationId, actingUserId, accessConnector.Id, enable: false);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.AccessConnectorDisabled && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessConnectorId == accessConnector.Id && e.AccessConnectorName == accessConnector.Name
            && e.ActorId == actingUserId));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.AccessConnectorDisabled && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessConnectorId == accessConnector.Id));
    }

    [Theory, BitAutoData]
    public async Task SetStatusAsync_Enable_EmitsEnabledKind(Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Disabled;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await sutProvider.Sut.SetStatusAsync(
            accessConnector.OrganizationId, actingUserId, accessConnector.Id, enable: true);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e =>
                e.Kind == AccessAuditEventKind.AccessConnectorEnabled && e.Phase == AccessAuditEventPhase.Outcome
                && e.AccessConnectorId == accessConnector.Id));
    }

    private static SutProvider<SetAccessConnectorStatusCommand> Setup()
    {
        var sutProvider = new SutProvider<SetAccessConnectorStatusCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
