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
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class EventIntegrationHandlerTests
{
    private const string _templateBase = "Date: #Date#, Type: #Type#, UserId: #UserId#";
    private const string _templateWithGroup = "Group: #GroupName#";
    private const string _templateWithOrganization = "Org: #OrganizationName#";
    private const string _templateWithUser = "#UserName#, #UserEmail#, #UserType#";
    private const string _templateWithActingUser = "#ActingUserName#, #ActingUserEmail#, #ActingUserType#";
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
    public async Task BuildContextAsync_ActingUserIdPresent_UsesCache(EventMessage eventMessage, OrganizationUserUserDetails actingUser)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithActingUser));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.OrganizationId ??= Guid.NewGuid();
        eventMessage.ActingUserId ??= Guid.NewGuid();

        cache.GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        ).Returns(actingUser);

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithActingUser);

        await cache.Received(1).GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        );

        Assert.Equal(actingUser, context.ActingUser);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_ActingUserIdNull_SkipsCache(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithActingUser));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.OrganizationId ??= Guid.NewGuid();
        eventMessage.ActingUserId = null;

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithActingUser);

        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        );
        Assert.Null(context.ActingUser);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_ActingUserOrganizationIdNull_SkipsCache(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithActingUser));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.OrganizationId = null;
        eventMessage.ActingUserId ??= Guid.NewGuid();

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithActingUser);

        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        );
        Assert.Null(context.ActingUser);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_GroupIdPresent_UsesCache(EventMessage eventMessage, Group group)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithGroup));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.GroupId ??= Guid.NewGuid();

        cache.GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<Group?>, CancellationToken, Task<Group?>>>()
        ).Returns(group);

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithGroup);

        await cache.Received(1).GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<Group?>, CancellationToken, Task<Group?>>>()
        );
        Assert.Equal(group, context.Group);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_GroupIdNull_SkipsCache(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithGroup));
        var cache = sutProvider.GetDependency<IFusionCache>();
        eventMessage.GroupId = null;

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithGroup);

        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<Group?>, CancellationToken, Task<Group?>>>()
        );
        Assert.Null(context.Group);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_OrganizationIdPresent_UsesCache(EventMessage eventMessage, Organization organization)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithOrganization));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.OrganizationId ??= Guid.NewGuid();

        cache.GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<Organization?>, CancellationToken, Task<Organization?>>>()
        ).Returns(organization);

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithOrganization);

        await cache.Received(1).GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<Organization?>, CancellationToken, Task<Organization?>>>()
        );
        Assert.Equal(organization, context.Organization);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_OrganizationIdNull_SkipsCache(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithOrganization));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.OrganizationId = null;

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithOrganization);

        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<Organization?>, CancellationToken, Task<Organization?>>>()
        );
        Assert.Null(context.Organization);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_UserIdPresent_UsesCache(EventMessage eventMessage, OrganizationUserUserDetails userDetails)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithUser));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.OrganizationId ??= Guid.NewGuid();
        eventMessage.UserId ??= Guid.NewGuid();

        cache.GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        ).Returns(userDetails);

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithUser);

        await cache.Received(1).GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        );

        Assert.Equal(userDetails, context.User);
    }


    [Theory, BitAutoData]
    public async Task BuildContextAsync_UserIdNull_SkipsCache(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithUser));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.OrganizationId = null;
        eventMessage.UserId ??= Guid.NewGuid();

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithUser);

        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        );

        Assert.Null(context.User);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_OrganizationUserIdNull_SkipsCache(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithUser));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.OrganizationId ??= Guid.NewGuid();
        eventMessage.UserId = null;

        var context = await sutProvider.Sut.BuildContextAsync(eventMessage, _templateWithUser);

        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        );

        Assert.Null(context.User);
    }

    [Theory, BitAutoData]
    public async Task BuildContextAsync_NoSpecialTokens_DoesNotCallAnyCache(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateWithUser));
        var cache = sutProvider.GetDependency<IFusionCache>();

        eventMessage.ActingUserId ??= Guid.NewGuid();
        eventMessage.GroupId ??= Guid.NewGuid();
        eventMessage.OrganizationId ??= Guid.NewGuid();
        eventMessage.UserId ??= Guid.NewGuid();

        await sutProvider.Sut.BuildContextAsync(eventMessage, _templateBase);

        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<Group?>, CancellationToken, Task<Group?>>>()
        );
        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<Organization?>, CancellationToken, Task<Organization?>>>()
        );
        await cache.DidNotReceive().GetOrSetAsync(
            key: Arg.Any<string>(),
            factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationUserUserDetails?>, CancellationToken, Task<OrganizationUserUserDetails?>>>()
        );
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
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetDetailsByOrganizationIdUserIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
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
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetDetailsByOrganizationIdUserIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
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
