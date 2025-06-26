﻿using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class EventIntegrationHandlerTests
{
    private const string _templateBase = "Date: #Date#, Type: #Type#, UserId: #UserId#";
    private const string _templateWithOrganization = "Org: #OrganizationName#";
    private const string _templateWithUser = "#UserName#, #UserEmail#";
    private const string _templateWithActingUser = "#ActingUserName#, #ActingUserEmail#";
    private const string _url = "https://localhost";
    private const string _url2 = "https://example.com";
    private readonly IEventIntegrationPublisher _eventIntegrationPublisher = Substitute.For<IEventIntegrationPublisher>();

    private SutProvider<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>> GetSutProvider(
        List<OrganizationIntegrationConfigurationDetails> configurations)
    {
        var configurationRepository = Substitute.For<IOrganizationIntegrationConfigurationRepository>();
        configurationRepository.GetConfigurationDetailsAsync(Arg.Any<Guid>(),
            IntegrationType.Webhook, Arg.Any<EventType>()).Returns(configurations);

        return new SutProvider<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>>()
            .SetDependency(configurationRepository)
            .SetDependency(_eventIntegrationPublisher)
            .SetDependency(IntegrationType.Webhook)
            .Create();
    }

    private static IntegrationMessage<WebhookIntegrationConfigurationDetails> expectedMessage(string template)
    {
        return new IntegrationMessage<WebhookIntegrationConfigurationDetails>()
        {
            IntegrationType = IntegrationType.Webhook,
            MessageId = "TestMessageId",
            Configuration = new WebhookIntegrationConfigurationDetails(_url),
            RenderedTemplate = template,
            RetryCount = 0,
            DelayUntilDate = null
        };
    }

    private static List<OrganizationIntegrationConfigurationDetails> NoConfigurations()
    {
        return [];
    }

    private static List<OrganizationIntegrationConfigurationDetails> OneConfiguration(string template)
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Url = _url });
        config.Template = template;

        return [config];
    }

    private static List<OrganizationIntegrationConfigurationDetails> TwoConfigurations(string template)
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Url = _url });
        config.Template = template;
        var config2 = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config2.Configuration = null;
        config2.IntegrationConfiguration = JsonSerializer.Serialize(new { Url = _url2 });
        config2.Template = template;

        return [config, config2];
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_BaseTemplateNoConfigurations_DoesNothing(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        Assert.Empty(_eventIntegrationPublisher.ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_BaseTemplateOneConfiguration_CallsProcessEventIntegrationAsync(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateBase));

        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.expectedMessage(
            $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
        );

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_ActingUserTemplate_LoadsUserFromRepository(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithActingUser));
        var user = Substitute.For<User>();
        user.Email = "test@example.com";
        user.Name = "Test";

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(user);
        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.expectedMessage($"{user.Name}, {user.Email}");

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IUserRepository>().Received(1).GetByIdAsync(eventMessage.ActingUserId ?? Guid.Empty);
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_OrganizationTemplate_LoadsOrganizationFromRepository(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithOrganization));
        var organization = Substitute.For<Organization>();
        organization.Name = "Test";

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(organization);
        await sutProvider.Sut.HandleEventAsync(eventMessage);

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());

        var expectedMessage = EventIntegrationHandlerTests.expectedMessage($"Org: {organization.Name}");

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(eventMessage.OrganizationId ?? Guid.Empty);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_UserTemplate_LoadsUserFromRepository(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithUser));
        var user = Substitute.For<User>();
        user.Email = "test@example.com";
        user.Name = "Test";

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(user);
        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.expectedMessage($"{user.Name}, {user.Email}");

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IUserRepository>().Received(1).GetByIdAsync(eventMessage.UserId ?? Guid.Empty);
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_BaseTemplateNoConfigurations_DoesNothing(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);
        Assert.Empty(_eventIntegrationPublisher.ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_BaseTemplateOneConfiguration_CallsProcessEventIntegrationAsync(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateBase));

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);

        foreach (var eventMessage in eventMessages)
        {
            var expectedMessage = EventIntegrationHandlerTests.expectedMessage(
                $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
            );
            await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
                AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        }
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_BaseTemplateTwoConfigurations_CallsProcessEventIntegrationAsyncMultipleTimes(
        List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(TwoConfigurations(_templateBase));

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);

        foreach (var eventMessage in eventMessages)
        {
            var expectedMessage = EventIntegrationHandlerTests.expectedMessage(
                $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
            );
            await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
                AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));

            expectedMessage.Configuration = new WebhookIntegrationConfigurationDetails(_url2);
            await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
                AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        }
    }
}
