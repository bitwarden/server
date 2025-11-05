using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
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
    private const string _templateWithGroup = "Group: #GroupName#";
    private const string _templateWithOrganization = "Org: #OrganizationName#";
    private const string _templateWithUser = "#UserName#, #UserEmail#, #UserType#";
    private const string _templateWithActingUser = "#ActingUserName#, #ActingUserEmail#, #ActingUserType#";
    private static readonly Guid _groupId = Guid.NewGuid();
    private static readonly Guid _organizationId = Guid.NewGuid();
    private static readonly Uri _uri = new Uri("https://localhost");
    private static readonly Uri _uri2 = new Uri("https://example.com");
    private readonly IEventIntegrationPublisher _eventIntegrationPublisher = Substitute.For<IEventIntegrationPublisher>();
    private readonly ILogger<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>> _logger =
        Substitute.For<ILogger<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>>>();

    private SutProvider<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>> GetSutProvider(
        List<OrganizationIntegrationConfigurationDetails> configurations)
    {
        var configurationCache = Substitute.For<IIntegrationConfigurationDetailsCache>();
        configurationCache.GetConfigurationDetails(Arg.Any<Guid>(),
            IntegrationType.Webhook, Arg.Any<EventType>()).Returns(configurations);

        return new SutProvider<EventIntegrationHandler<WebhookIntegrationConfigurationDetails>>()
            .SetDependency(configurationCache)
            .SetDependency(_eventIntegrationPublisher)
            .SetDependency(IntegrationType.Webhook)
            .SetDependency(_logger)
            .Create();
    }

    private static IntegrationMessage<WebhookIntegrationConfigurationDetails> ExpectedMessage(string template)
    {
        return new IntegrationMessage<WebhookIntegrationConfigurationDetails>()
        {
            IntegrationType = IntegrationType.Webhook,
            MessageId = "TestMessageId",
            OrganizationId = _organizationId.ToString(),
            Configuration = new WebhookIntegrationConfigurationDetails(_uri),
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
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Uri = _uri });
        config.Template = template;

        return [config];
    }

    private static List<OrganizationIntegrationConfigurationDetails> TwoConfigurations(string template)
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Uri = _uri });
        config.Template = template;
        var config2 = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config2.Configuration = null;
        config2.IntegrationConfiguration = JsonSerializer.Serialize(new { Uri = _uri2 });
        config2.Template = template;

        return [config, config2];
    }

    private static List<OrganizationIntegrationConfigurationDetails> InvalidFilterConfiguration()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Uri = _uri });
        config.Template = _templateBase;
        config.Filters = "Invalid Configuration!";

        return [config];
    }

    private static List<OrganizationIntegrationConfigurationDetails> ValidFilterConfiguration()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Uri = _uri });
        config.Template = _templateBase;
        config.Filters = JsonSerializer.Serialize(new IntegrationFilterGroup());

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
    public async Task HandleEventAsync_NoOrganizationId_DoesNothing(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateBase));
        eventMessage.OrganizationId = null;

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        Assert.Empty(_eventIntegrationPublisher.ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_BaseTemplateOneConfiguration_PublishesIntegrationMessage(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateBase));
        eventMessage.OrganizationId = _organizationId;

        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage(
            $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
        );

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetDetailsByOrganizationUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_BaseTemplateTwoConfigurations_PublishesIntegrationMessages(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(TwoConfigurations(_templateBase));
        eventMessage.OrganizationId = _organizationId;

        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage(
            $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
        );
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));

        expectedMessage.Configuration = new WebhookIntegrationConfigurationDetails(_uri2);
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetDetailsByOrganizationUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_ActingUserTemplate_LoadsUserFromRepository(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithActingUser));
        var user = Substitute.For<OrganizationUserUserDetails>();
        user.Email = "test@example.com";
        user.Name = "Test";
        eventMessage.OrganizationId = _organizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByOrganizationUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(user);
        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage($"{user.Name}, {user.Email}, {user.Type}");

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetDetailsByOrganizationUserAsync(Arg.Any<Guid>(), eventMessage.ActingUserId ?? Guid.Empty);
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_GroupTemplate_LoadsGroupFromRepository(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithGroup));
        var group = Substitute.For<Group>();
        group.Name = "Test";
        eventMessage.GroupId = _groupId;
        eventMessage.OrganizationId = _organizationId;

        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(group);
        await sutProvider.Sut.HandleEventAsync(eventMessage);

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());

        var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage($"Group: {group.Name}");

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IGroupRepository>().Received(1).GetByIdAsync(eventMessage.GroupId ?? Guid.Empty);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetDetailsByOrganizationUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_OrganizationTemplate_LoadsOrganizationFromRepository(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithOrganization));
        var organization = Substitute.For<Organization>();
        organization.Name = "Test";
        eventMessage.OrganizationId = _organizationId;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(organization);
        await sutProvider.Sut.HandleEventAsync(eventMessage);

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());

        var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage($"Org: {organization.Name}");

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(eventMessage.OrganizationId ?? Guid.Empty);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetDetailsByOrganizationUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_UserTemplate_LoadsUserFromRepository(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithUser));
        var user = Substitute.For<OrganizationUserUserDetails>();
        user.Email = "test@example.com";
        user.Name = "Test";
        eventMessage.OrganizationId = _organizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByOrganizationUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(user);
        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage($"{user.Name}, {user.Email}, {user.Type}");

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetDetailsByOrganizationUserAsync(Arg.Any<Guid>(), eventMessage.UserId ?? Guid.Empty);
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
        eventMessage.OrganizationId = _organizationId;

        await sutProvider.Sut.HandleEventAsync(eventMessage);

        var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage(
            $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
        );

        Assert.Single(_eventIntegrationPublisher.ReceivedCalls());
        await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
            AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId" })));
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_InvalidFilter_LogsErrorDoesNothing(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(InvalidFilterConfiguration());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        Assert.Empty(_eventIntegrationPublisher.ReceivedCalls());
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<JsonException>(),
            Arg.Any<Func<object, Exception, string>>());
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
            var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage(
                $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
            );
            await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(
                AssertHelper.AssertPropertyEqual(expectedMessage, new[] { "MessageId", "OrganizationId" })));
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
            var expectedMessage = EventIntegrationHandlerTests.ExpectedMessage(
                $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}"
            );
            await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(AssertHelper.AssertPropertyEqual(
                expectedMessage, new[] { "MessageId", "OrganizationId" })));

            expectedMessage.Configuration = new WebhookIntegrationConfigurationDetails(_uri2);
            await _eventIntegrationPublisher.Received(1).PublishAsync(Arg.Is(AssertHelper.AssertPropertyEqual(
                expectedMessage, new[] { "MessageId", "OrganizationId" })));
        }
    }
}
