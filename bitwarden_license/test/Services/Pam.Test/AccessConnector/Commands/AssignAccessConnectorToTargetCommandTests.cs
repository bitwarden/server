using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
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
    public async Task AssignAsync_OrganizationWithoutPam_ThrowsBadRequest(Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        target.OrganizationId = accessConnector.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(accessConnector.OrganizationId)
            .Returns(new OrganizationAbility { Id = accessConnector.OrganizationId, UsePam = false });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id));

        Assert.Equal("Your organization's plan does not include Privileged Controls.", exception.Message);
        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
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
        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .GetManyDetailsByOrganizationIdAsync(default);
        await sutProvider.GetDependency<ICanEditRotationCiphersQuery>().DidNotReceiveWithAnyArgs()
            .CanEditAllAsync(default, default!);
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
        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .GetManyDetailsByOrganizationIdAsync(default);
        await sutProvider.GetDependency<ICanEditRotationCiphersQuery>().DidNotReceiveWithAnyArgs()
            .CanEditAllAsync(default, default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_TargetRotatesACipherNotEditable_ThrowsBadRequest(
        Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target, PamRotationConfigDetails config)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        target.OrganizationId = accessConnector.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        config.TargetSystemId = target.Id;
        sutProvider.GetDependency<IPamRotationConfigRepository>()
            .GetManyDetailsByOrganizationIdAsync(accessConnector.OrganizationId)
            .Returns(new List<PamRotationConfigDetails> { config });
        sutProvider.GetDependency<ICanEditRotationCiphersQuery>()
            .CanEditAllAsync(accessConnector.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { config.CipherId })))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AssignAsync(
                accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id));

        Assert.Equal("You need edit access to every item this target system rotates.", exception.Message);
        await sutProvider.GetDependency<IPamAccessConnectorRepository>().DidNotReceiveWithAnyArgs()
            .CreateAssignmentAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task AssignAsync_ChecksOnlyTheTargetsCiphers(
        Guid actingUserId, PamAccessConnector accessConnector, PamTargetSystem target,
        PamRotationConfigDetails onTarget, PamRotationConfigDetails onAnotherTarget)
    {
        var sutProvider = Setup();
        accessConnector.Status = PamAccessConnectorStatus.Enabled;
        target.OrganizationId = accessConnector.OrganizationId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamAccessConnectorRepository>().GetByIdAsync(accessConnector.Id)
            .Returns(accessConnector);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        onTarget.TargetSystemId = target.Id;
        sutProvider.GetDependency<IPamRotationConfigRepository>()
            .GetManyDetailsByOrganizationIdAsync(accessConnector.OrganizationId)
            .Returns(new List<PamRotationConfigDetails> { onTarget, onAnotherTarget });

        await sutProvider.Sut.AssignAsync(accessConnector.OrganizationId, actingUserId, accessConnector.Id, target.Id);

        await sutProvider.GetDependency<ICanEditRotationCiphersQuery>().Received(1)
            .CanEditAllAsync(accessConnector.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { onTarget.CipherId })));
        await sutProvider.GetDependency<IPamAccessConnectorRepository>().ReceivedWithAnyArgs(1)
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
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(Arg.Any<Guid>())
            .Returns(call => new OrganizationAbility { Id = call.Arg<Guid>(), UsePam = true });
        sutProvider.GetDependency<IPamRotationConfigRepository>()
            .GetManyDetailsByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(new List<PamRotationConfigDetails>());
        sutProvider.GetDependency<ICanEditRotationCiphersQuery>()
            .CanEditAllAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
        return sutProvider;
    }
}
