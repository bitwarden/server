using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation;
using Bit.Services.Pam.Rotation.Commands;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class TriggerRotationCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task TriggerAsync_ConfigMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.TriggerAsync(organizationId, actingUserId, configId));

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs().OfferAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.TriggerAsync(Guid.NewGuid(), actingUserId, details.Id));

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs().OfferAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_TargetMissing_ThrowsNotFound(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(details.TargetSystemId)
            .Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.TriggerAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs().OfferAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_Paused_ThrowsBadRequest(Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target)
    {
        var sutProvider = Setup();
        SetupOfferable(sutProvider, details, target);
        details.Enabled = false;

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TriggerAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs().OfferAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_ManualTarget_ThrowsBadRequest(Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target)
    {
        var sutProvider = Setup();
        SetupOfferable(sutProvider, details, target);
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TriggerAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs().OfferAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_TargetDisabled_ThrowsBadRequest(Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target)
    {
        var sutProvider = Setup();
        SetupOfferable(sutProvider, details, target);
        target.Status = PamTargetSystemStatus.Disabled;

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TriggerAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs().OfferAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_ActiveJob_ThrowsBadRequest(Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target)
    {
        var sutProvider = Setup();
        SetupOfferable(sutProvider, details, target);
        details.HasActiveJob = true;

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TriggerAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs().OfferAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_WithinCooldown_ThrowsBadRequest(Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target)
    {
        var sutProvider = Setup();
        SetupOfferable(sutProvider, details, target);
        details.LastRotationAt = _now.AddSeconds(-30); // inside the 1-minute cooldown

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TriggerAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs().OfferAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_CooldownElapsed_DelegatesToOfferWithOnDemand(
        Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target)
    {
        var sutProvider = Setup();
        SetupOfferable(sutProvider, details, target);
        details.LastRotationAt = _now.AddMinutes(-5); // past the 1-minute cooldown

        await sutProvider.Sut.TriggerAsync(details.OrganizationId, actingUserId, details.Id);

        await sutProvider.GetDependency<IOfferRotationCommand>().Received(1)
            .OfferAsync(details.Id, PamRotationSource.OnDemand);
    }

    [Theory, BitAutoData]
    public async Task TriggerAsync_NeverRotated_DelegatesToOfferWithOnDemand(
        Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target)
    {
        var sutProvider = Setup();
        SetupOfferable(sutProvider, details, target);
        details.LastRotationAt = null; // no prior rotation, so no cooldown

        await sutProvider.Sut.TriggerAsync(details.OrganizationId, actingUserId, details.Id);

        await sutProvider.GetDependency<IOfferRotationCommand>().Received(1)
            .OfferAsync(details.Id, PamRotationSource.OnDemand);
    }

    /// <summary>An offerable baseline: enabled, automatic method, active target, no active job.</summary>
    private static void SetupOfferable(
        SutProvider<TriggerRotationCommand> sutProvider, PamRotationConfigDetails details, PamTargetSystem target)
    {
        details.Enabled = true;
        details.TargetSystemMethod = PamTargetSystemMethod.Automatic;
        details.HasActiveJob = false;
        details.LastRotationAt = null;
        target.Status = PamTargetSystemStatus.Active;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(details.TargetSystemId).Returns(target);
    }

    private static SutProvider<TriggerRotationCommand> Setup()
    {
        var sutProvider = new SutProvider<TriggerRotationCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value
            .Returns(new PamRotationOptions { OnDemandCooldown = TimeSpan.FromMinutes(1) });
        return sutProvider;
    }
}
