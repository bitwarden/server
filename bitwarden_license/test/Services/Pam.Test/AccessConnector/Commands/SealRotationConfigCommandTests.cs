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
public class SealRotationConfigCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task SealAsync_SealedDataMissing_ThrowsBadRequest(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SealAsync(organizationId, actingUserId, configId, " "));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SealAsync_ConfigMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid configId, string sealedData)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SealAsync(organizationId, actingUserId, configId, sealedData));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SealAsync_WrongOrg_ThrowsNotFound(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SealAsync(Guid.NewGuid(), actingUserId, details.Id, sealedData));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SealAsync_ActiveJob_ThrowsBadRequest(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData)
    {
        var sutProvider = Setup();
        details.HasActiveJob = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SealAsync(details.OrganizationId, actingUserId, details.Id, sealedData));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SealAsync_HappyPath_ReplacesSealedData(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        var result = await sutProvider.Sut.SealAsync(details.OrganizationId, actingUserId, details.Id, sealedData);

        Assert.Equal(sealedData, result.SealedData);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.SealedData == sealedData && c.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task SealAsync_HappyPath_EmitsAttemptThenOutcome(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.SealAsync(details.OrganizationId, actingUserId, details.Id, sealedData);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationAccountUpdated && e.Phase == AccessAuditEventPhase.Attempt
            && e.RotationConfigId == details.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationAccountUpdated && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == details.Id));
    }

    private static SutProvider<SealRotationConfigCommand> Setup()
    {
        var sutProvider = new SutProvider<SealRotationConfigCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
