using Bit.Core.AdminConsole.OrganizationAuth.Models;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationAuth.Models;

[SutProviderCustomize]
public class BatchAuthRequestUpdateProcessorTests
{
    [Theory]
    [BitAutoData]
    public void Process_NoProcessors_Handled(
        IEnumerable<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Action<Exception> errorHandler
    )
    {
        var sut = new BatchAuthRequestUpdateProcessor(null, updates, configuration);
        sut.Process(errorHandler);
    }

    [Theory]
    [BitAutoData]
    public void Process_BadInput_CallsHandler(
        List<OrganizationAdminAuthRequest> authRequests,
        IEnumerable<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration
    )
    {
        // An already approved auth request should break the processor
        // immediately.
        authRequests[0].Approved = true;
        var sut = new BatchAuthRequestUpdateProcessor(authRequests, updates, configuration);
        var errorHandler = Substitute.For<Action<Exception>>();
        sut.Process(errorHandler);
        errorHandler.ReceivedWithAnyArgs()(new AuthRequestUpdateProcessingException());
    }

    [Theory]
    [BitAutoData]
    public void Process_ValidInput_Works(
        List<OrganizationAdminAuthRequest> authRequests,
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Action<Exception> errorHandler
    )
    {
        (authRequests[0], updates[0], configuration) = UnrespondAndEnsureValid(authRequests[0], updates[0], configuration);
        var sut = new BatchAuthRequestUpdateProcessor(authRequests, updates, configuration);
        Assert.NotEmpty(sut.Processors);
        sut.Process(errorHandler);
        Assert.NotEmpty(sut.Processors.Where(p => p.ProcessedAuthRequest != null));
    }

    [Theory]
    [BitAutoData]
    public async Task Save_NoProcessedAuthRequests_IsHandled(
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Func<IEnumerable<AuthRequest>, Task> saveCallback
    )
    {
        var sut = new BatchAuthRequestUpdateProcessor(null, updates, configuration);
        Assert.Empty(sut.Processors);
        await sut.Save(saveCallback);
    }

    [Theory]
    [BitAutoData]
    public async Task Save_ProcessedAuthRequests_IsHandled(
        List<OrganizationAdminAuthRequest> authRequests,
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Action<Exception> errorHandler
    )
    {
        (authRequests[0], updates[0], configuration) = UnrespondAndEnsureValid(authRequests[0], updates[0], configuration);
        var sut = new BatchAuthRequestUpdateProcessor(authRequests, updates, configuration);
        var saveCallback = Substitute.For<Func<IEnumerable<OrganizationAdminAuthRequest>, Task>>();
        await sut.Process(errorHandler).Save(saveCallback);
        await saveCallback.ReceivedWithAnyArgs()(Arg.Any<IEnumerable<OrganizationAdminAuthRequest>>());
    }

    [Theory]
    [BitAutoData]
    public async Task SendPushNotifications_NoProcessors_IsHandled
    (
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Func<AuthRequest, Task> callback
    )
    {
        var sut = new BatchAuthRequestUpdateProcessor(null, updates, configuration);
        Assert.Empty(sut.Processors);
        await sut.SendPushNotifications(callback);
    }

    [Theory]
    [BitAutoData]
    public async Task SendPushNotifications_HasProcessors_Sends
    (
        List<OrganizationAdminAuthRequest> authRequests,
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Action<Exception> errorHandler
    )
    {
        (authRequests[0], updates[0], configuration) = UnrespondAndEnsureValid(authRequests[0], updates[0], configuration);
        var sut = new BatchAuthRequestUpdateProcessor(authRequests, updates, configuration);
        var callback = Substitute.For<Func<OrganizationAdminAuthRequest, Task>>();
        await sut.Process(errorHandler).SendPushNotifications(callback);
        await callback.ReceivedWithAnyArgs()(Arg.Any<OrganizationAdminAuthRequest>());
    }

    [Theory]
    [BitAutoData]
    public async Task SendApprovalEmailsForProcessedRequests_NoProcessors_IsHandled
    (
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Func<AuthRequest, string, Task> callback
    )
    {
        var sut = new BatchAuthRequestUpdateProcessor(null, updates, configuration);
        Assert.Empty(sut.Processors);
        await sut.SendApprovalEmailsForProcessedRequests(callback);
    }

    [Theory]
    [BitAutoData]
    public async Task SendApprovalEmailsForProcessedRequests_HasProcessors_Sends
    (
        List<OrganizationAdminAuthRequest> authRequests,
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Action<Exception> errorHandler
    )
    {
        (authRequests[0], updates[0], configuration) = UnrespondAndEnsureValid(authRequests[0], updates[0], configuration);
        var sut = new BatchAuthRequestUpdateProcessor(authRequests, updates, configuration);
        var callback = Substitute.For<Func<OrganizationAdminAuthRequest, string, Task>>();
        await sut.Process(errorHandler).SendApprovalEmailsForProcessedRequests(callback);
        await callback.ReceivedWithAnyArgs()(Arg.Any<OrganizationAdminAuthRequest>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task LogOrganizationEventsForProcessedRequests_NoProcessedAuthRequests_IsHandled
    (
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration
    )
    {
        var sut = new BatchAuthRequestUpdateProcessor(null, updates, configuration);
        var callback = Substitute.For<Func<IEnumerable<(OrganizationAdminAuthRequest, EventType)>, Task>>();
        Assert.Empty(sut.Processors);
        await sut.LogOrganizationEventsForProcessedRequests(callback);
        await callback.DidNotReceiveWithAnyArgs()(Arg.Any<IEnumerable<(OrganizationAdminAuthRequest, EventType)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task LogOrganizationEventsForProcessedRequests_HasProcessedAuthRequests_IsHandled
    (
        List<OrganizationAdminAuthRequest> authRequests,
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration,
        Action<Exception> errorHandler
    )
    {
        (authRequests[0], updates[0], configuration) = UnrespondAndEnsureValid(authRequests[0], updates[0], configuration);
        var sut = new BatchAuthRequestUpdateProcessor(authRequests, updates, configuration);
        var callback = Substitute.For<Func<IEnumerable<(OrganizationAdminAuthRequest, EventType)>, Task>>();
        await sut.Process(errorHandler).LogOrganizationEventsForProcessedRequests(callback);
        await callback.ReceivedWithAnyArgs()(Arg.Any<IEnumerable<(OrganizationAdminAuthRequest, EventType)>>());
    }

    private (
        T authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration ProcessorConfiguration
    ) UnrespondAndEnsureValid<T>(
        T authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    ) where T : AuthRequest
    {
        authRequest.Id = update.Id;
        authRequest.OrganizationId = processorConfiguration.OrganizationId;
        authRequest.Key = null;
        authRequest.Approved = null;
        authRequest.ResponseDate = null;
        authRequest.AuthenticationDate = null;
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-1);
        processorConfiguration.AuthRequestExpiresAfter = new TimeSpan(1, 0, 0);

        update.Approved = true;
        update.Key = "key";
        return (authRequest, update, processorConfiguration);
    }
}
