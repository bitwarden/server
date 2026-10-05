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
public class DeleteAccessConnectorCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task DeleteAsync_AccessConnectorMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid accessConnectorId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnectorId)
            .Returns((PamAccessConnector?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, actingUserId, accessConnectorId));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        // accessConnector.OrganizationId is an unrelated AutoFixture Guid -- a cross-org lookup must 404, never leak
        // existence.
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), actingUserId, accessConnector.Id));

        await sutProvider.GetDependency<IPamAccessConnectorRepository>()
            .DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_DeletesTheAccessConnectorThroughTheRepositoryCascade(
        Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await sutProvider.Sut.DeleteAsync(accessConnector.OrganizationId, actingUserId, accessConnector.Id);

        await sutProvider.GetDependency<IPamAccessConnectorRepository>().Received(1).DeleteAsync(accessConnector);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_EmitsAttemptThenOutcome(Guid actingUserId, PamAccessConnector accessConnector)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);

        await sutProvider.Sut.DeleteAsync(accessConnector.OrganizationId, actingUserId, accessConnector.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.AccessConnectorDeleted && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessConnectorId == accessConnector.Id && e.AccessConnectorName == accessConnector.Name
            && e.ActorId == actingUserId));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.AccessConnectorDeleted && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessConnectorId == accessConnector.Id));
    }

    private static SutProvider<DeleteAccessConnectorCommand> Setup()
    {
        var sutProvider = new SutProvider<DeleteAccessConnectorCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
