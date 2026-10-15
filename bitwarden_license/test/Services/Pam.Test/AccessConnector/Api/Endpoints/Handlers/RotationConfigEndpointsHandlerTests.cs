using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Api.Endpoints.Handlers;

/// <summary>
/// ManageAccessConnectorRequirement only proves the caller manages rotation in the organization, so each config write
/// also needs edit access to the config's cipher.
/// </summary>
[SutProviderCustomize]
public class RotationConfigEndpointsHandlerTests
{
    [Theory, BitAutoData]
    public async Task Post_CipherNotEditable_ThrowsNotFound(
        Guid organizationId, CreateRotationConfigRequestModel model,
        SutProvider<RotationConfigEndpointsHandler> sutProvider)
    {
        SetCanEdit(sutProvider, organizationId, model.CipherId, false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Post(organizationId, model));

        Assert.Empty(sutProvider.GetDependency<ICreateRotationConfigCommand>().ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task Post_CipherEditable_CreatesTheConfig(
        Guid organizationId, CreateRotationConfigRequestModel model, PamRotationConfigDetails details,
        SutProvider<RotationConfigEndpointsHandler> sutProvider)
    {
        SetCanEdit(sutProvider, organizationId, model.CipherId, true);
        SetUpWrites(sutProvider, details);

        await sutProvider.Sut.Post(organizationId, model);

        await sutProvider.GetDependency<ICreateRotationConfigCommand>().Received(1).CreateAsync(
            organizationId, Arg.Any<Guid>(), model.CipherId, model.TargetSystemId, model.SealedData,
            model.WrappedKey, model.ScheduleCron, model.RotateOnAccessEnd);
    }

    [Theory]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Put))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Pause))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Resume))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Rotate))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.RecordManual))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Delete))]
    public async Task ConfigWrite_CipherNotEditable_ThrowsNotFound(
        string write, PamRotationConfig config, SutProvider<RotationConfigEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        SetCanEdit(sutProvider, config.OrganizationId, config.CipherId, false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => InvokeAsync(sutProvider.Sut, write, config.OrganizationId, config.Id));

        Assert.Empty(CommandFor(sutProvider, write).ReceivedCalls());
    }

    [Theory]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Put))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Pause))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Resume))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Rotate))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.RecordManual))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Delete))]
    public async Task ConfigWrite_ConfigMissing_ThrowsNotFound(
        string write, Guid organizationId, Guid configId, SutProvider<RotationConfigEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(configId).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(
            () => InvokeAsync(sutProvider.Sut, write, organizationId, configId));

        Assert.Empty(CommandFor(sutProvider, write).ReceivedCalls());
    }

    [Theory]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Put))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Pause))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Resume))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Rotate))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.RecordManual))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Delete))]
    public async Task ConfigWrite_ConfigInAnotherOrganization_ThrowsNotFound(
        string write, PamRotationConfig config, Guid otherOrganizationId,
        SutProvider<RotationConfigEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        SetCanEdit(sutProvider, otherOrganizationId, config.CipherId, true);

        await Assert.ThrowsAsync<NotFoundException>(
            () => InvokeAsync(sutProvider.Sut, write, otherOrganizationId, config.Id));

        Assert.Empty(CommandFor(sutProvider, write).ReceivedCalls());
    }

    [Theory]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Put))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Pause))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Resume))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Rotate))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.RecordManual))]
    [BitAutoData(nameof(RotationConfigEndpointsHandler.Delete))]
    public async Task ConfigWrite_CipherEditable_RunsTheCommand(
        string write, PamRotationConfig config, PamRotationConfigDetails details,
        SutProvider<RotationConfigEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        SetCanEdit(sutProvider, config.OrganizationId, config.CipherId, true);
        SetUpWrites(sutProvider, details);

        await InvokeAsync(sutProvider.Sut, write, config.OrganizationId, config.Id);

        Assert.Single(CommandFor(sutProvider, write).ReceivedCalls());
    }

    private static void SetCanEdit(
        SutProvider<RotationConfigEndpointsHandler> sutProvider, Guid organizationId, Guid cipherId, bool canEdit)
    {
        sutProvider.GetDependency<ICanEditRotationCiphersQuery>()
            .CanEditAllAsync(organizationId, Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { cipherId })))
            .Returns(canEdit);
    }

    /// <summary>Lets a write reach its command and re-read the detail view.</summary>
    private static void SetUpWrites(
        SutProvider<RotationConfigEndpointsHandler> sutProvider, PamRotationConfigDetails details)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(Guid.NewGuid());
        sutProvider.GetDependency<ICreateRotationConfigCommand>()
            .CreateAsync(default, default, default, default, default!, default!, default, default)
            .ReturnsForAnyArgs(details);
        sutProvider.GetDependency<IUpdateRotationConfigCommand>()
            .UpdateAsync(default, default, default, default!, default!, default, default)
            .ReturnsForAnyArgs(details);
        sutProvider.GetDependency<IGetRotationConfigDetailsQuery>()
            .GetAsync(default, default)
            .ReturnsForAnyArgs(new PamRotationConfigHistory(details, []));
    }

    private static Task InvokeAsync(RotationConfigEndpointsHandler handler, string write, Guid orgId, Guid id) =>
        write switch
        {
            nameof(RotationConfigEndpointsHandler.Put) =>
                handler.Put(orgId, id, new UpdateRotationConfigRequestModel { SealedData = "sealed" }),
            nameof(RotationConfigEndpointsHandler.Pause) => handler.Pause(orgId, id),
            nameof(RotationConfigEndpointsHandler.Resume) => handler.Resume(orgId, id),
            nameof(RotationConfigEndpointsHandler.Rotate) => handler.Rotate(orgId, id),
            nameof(RotationConfigEndpointsHandler.RecordManual) => handler.RecordManual(orgId, id),
            nameof(RotationConfigEndpointsHandler.Delete) => handler.Delete(orgId, id),
            _ => throw new ArgumentOutOfRangeException(nameof(write)),
        };

    private static object CommandFor(SutProvider<RotationConfigEndpointsHandler> sutProvider, string write) =>
        write switch
        {
            nameof(RotationConfigEndpointsHandler.Put) => sutProvider.GetDependency<IUpdateRotationConfigCommand>(),
            nameof(RotationConfigEndpointsHandler.Pause) => sutProvider.GetDependency<IPauseRotationCommand>(),
            nameof(RotationConfigEndpointsHandler.Resume) => sutProvider.GetDependency<IResumeRotationCommand>(),
            nameof(RotationConfigEndpointsHandler.Rotate) => sutProvider.GetDependency<ITriggerRotationCommand>(),
            nameof(RotationConfigEndpointsHandler.RecordManual) =>
                sutProvider.GetDependency<IRecordManualRotationCommand>(),
            nameof(RotationConfigEndpointsHandler.Delete) => sutProvider.GetDependency<IDeleteRotationConfigCommand>(),
            _ => throw new ArgumentOutOfRangeException(nameof(write)),
        };
}
