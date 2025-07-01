using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Logging;
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
    private static readonly Uri _uri = new Uri("https://localhost");
    private static readonly Uri _uri2 = new Uri("https://example.com");
    private readonly IEventIntegrationPublisher _eventIntegrationPublisher = Substitute.For<IEventIntegrationPublisher>();
    private readonly ILogger<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>> _logger =
        Substitute.For<ILogger<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>>>();

    private SutProvider<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>> GetSutProvider(
        List<CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>> configurations)
    {
        var detailsCache = Substitute.For<IIntegrationConfigurationDetailsCache>();
        detailsCache.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(Arg.Any<Guid>(),
            IntegrationType.Webhook, Arg.Any<EventType>()).Returns(configurations);

        return new SutProvider<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>>()
            .SetDependency(detailsCache)
            .SetDependency(_eventIntegrationPublisher)
            .SetDependency(IntegrationType.Webhook)
            .SetDependency(_logger)
            .Create();
    }

    private static IntegrationMessage<WebhookIntegrationConfigurationDetails> expectedMessage(string template)
    {
        return new IntegrationMessage<WebhookIntegrationConfigurationDetails>()
        {
            IntegrationType = IntegrationType.Webhook,
            MessageId = "TestMessageId",
            Configuration = new WebhookIntegrationConfigurationDetails(_uri),
            RenderedTemplate = template,
            RetryCount = 0,
            DelayUntilDate = null
        };
    }

    private static List<CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>> NoConfigurations()
    {
        return [];
    }

    private static List<CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>> OneConfiguration(string template)
    {
        var config = new CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>()
        {
            Configuration = new WebhookIntegrationConfigurationDetails(Uri: _uri),
            FilterGroup = null,
            Template = template
        };

        return [config];
    }

    private static List<CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>> TwoConfigurations(string template)
    {
        var config = new CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>()
        {
            Configuration = new WebhookIntegrationConfigurationDetails(Uri: _uri),
            FilterGroup = null,
            Template = template
        };
        var config2 = new CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>()
        {
            Configuration = new WebhookIntegrationConfigurationDetails(Uri: _uri2),
            FilterGroup = null,
            Template = template
        };

        return [config, config2];
    }

    private static List<CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>> ValidFilterConfiguration()
    {
        var config = new CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>()
        {
            Configuration = new WebhookIntegrationConfigurationDetails(Uri: _uri),
            FilterGroup = new IntegrationFilterGroup(),
            Template = _templateBase
        };

        return [config];
    }


    [Theory, BitAutoData]
    public async Task HandleEventAsync_BaseTemplateNoConfigurations_DoesNothing(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        Assert.Empty(_eventIntegrationPublisher.ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_BaseTemplateOneConfiguration_PublishesIntegrationMessage(EventMessage eventMessage)
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
    public async Task HandleEventAsync_BaseTemplateTwoConfigurations_PublishesIntegrationMessages(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(TwoConfigurations(_templateBase));

        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.expectedMessage(
            $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
        );
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));

        expectedMessage.Configuration = new WebhookIntegrationConfigurationDetails(_uri2);
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
    public async Task HandleEventAsync_FilterReturnsFalse_DoesNothing(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(ValidFilterConfiguration());
        sutProvider.GetDependency<IIntegrationFilterService>().EvaluateFilterGroup(
            Arg.Any<IntegrationFilterGroup>(), Arg.Any<EventMessage>()).Returns(false);

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        Assert.Empty(_eventIntegrationPublisher.ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_FilterReturnsTrue_PublishesIntegrationMessage(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(ValidFilterConfiguration());
        sutProvider.GetDependency<IIntegrationFilterService>().EvaluateFilterGroup(
            Arg.Any<IntegrationFilterGroup>(), Arg.Any<EventMessage>()).Returns(true);

        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.expectedMessage(
            $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
        );

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_BaseTemplateNoConfigurations_DoesNothing(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);
        Assert.Empty(_eventIntegrationPublisher.ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_BaseTemplateOneConfiguration_PublishesIntegrationMessages(List<EventMessage> eventMessages)
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
    public async Task HandleManyEventsAsync_BaseTemplateTwoConfigurations_PublishesIntegrationMessages(
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

            expectedMessage.Configuration = new WebhookIntegrationConfigurationDetails(_uri2);
            await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
                AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        }
    }
}
