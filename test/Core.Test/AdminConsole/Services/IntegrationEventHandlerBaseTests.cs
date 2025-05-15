using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class IntegrationEventHandlerBaseEventHandlerTests
{
    private const string _templateBase = "Date: #Date#, Type: #Type#, UserId: #UserId#";
    private const string _templateWithOrganization = "Org: #OrganizationName#";
    private const string _templateWithUser = "#UserName#, #UserEmail#";
    private const string _templateWithActingUser = "#ActingUserName#, #ActingUserEmail#";
    private const string _url = "https://localhost";

    private SutProvider<TestIntegrationEventHandlerBase> GetSutProvider(
        List<OrganizationIntegrationConfigurationDetails> configurations)
    {
        var configurationRepository = Substitute.For<IOrganizationIntegrationConfigurationRepository>();
        configurationRepository.GetConfigurationDetailsAsync(Arg.Any<Guid>(),
            IntegrationType.Webhook, Arg.Any<EventType>()).Returns(configurations);

        return new SutProvider<TestIntegrationEventHandlerBase>()
            .SetDependency(configurationRepository)
            .Create();
    }

    private static List<OrganizationIntegrationConfigurationDetails> NoConfigurations()
    {
        return [];
    }

    private static List<OrganizationIntegrationConfigurationDetails> OneConfiguration(string template)
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { url = _url });
        config.Template = template;

        return [config];
    }

    private static List<OrganizationIntegrationConfigurationDetails> TwoConfigurations(string template)
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { url = _url });
        config.Template = template;
        var config2 = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config2.Configuration = null;
        config2.IntegrationConfiguration = JsonSerializer.Serialize(new { url = _url });
        config2.Template = template;

        return [config, config2];
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_BaseTemplateNoConfigurations_DoesNothing(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        Assert.Empty(sutProvider.Sut.CapturedCalls);
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_BaseTemplateOneConfiguration_CallsProcessEventIntegrationAsync(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateBase));

        await sutProvider.Sut.HandleEventAsync(eventMessage);

        Assert.Single(sutProvider.Sut.CapturedCalls);

        var expectedTemplate = $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}";

        Assert.Equal(expectedTemplate, sutProvider.Sut.CapturedCalls.Single().RenderedTemplate);
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

        Assert.Single(sutProvider.Sut.CapturedCalls);

        var expectedTemplate = $"{user.Name}, {user.Email}";
        Assert.Equal(expectedTemplate, sutProvider.Sut.CapturedCalls.Single().RenderedTemplate);
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

        Assert.Single(sutProvider.Sut.CapturedCalls);

        var expectedTemplate = $"Org: {organization.Name}";
        Assert.Equal(expectedTemplate, sutProvider.Sut.CapturedCalls.Single().RenderedTemplate);
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

        Assert.Single(sutProvider.Sut.CapturedCalls);

        var expectedTemplate = $"{user.Name}, {user.Email}";
        Assert.Equal(expectedTemplate, sutProvider.Sut.CapturedCalls.Single().RenderedTemplate);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IUserRepository>().Received(1).GetByIdAsync(eventMessage.UserId ?? Guid.Empty);
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_BaseTemplateNoConfigurations_DoesNothing(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);
        Assert.Empty(sutProvider.Sut.CapturedCalls);
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_BaseTemplateOneConfiguration_CallsProcessEventIntegrationAsync(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(OneConfiguration(_templateBase));

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);

        Assert.Equal(eventMessages.Count, sutProvider.Sut.CapturedCalls.Count);
        var index = 0;
        foreach (var call in sutProvider.Sut.CapturedCalls)
        {
            var expected = eventMessages[index];
            var expectedTemplate = $"Date: {expected.Date}, Type: {expected.Type}, UserId: {expected.UserId}";

            Assert.Equal(expectedTemplate, call.RenderedTemplate);
            index++;
        }
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_BaseTemplateTwoConfigurations_CallsProcessEventIntegrationAsyncMultipleTimes(
        List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(TwoConfigurations(_templateBase));

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);

        Assert.Equal(eventMessages.Count * 2, sutProvider.Sut.CapturedCalls.Count);

        var capturedCalls = sutProvider.Sut.CapturedCalls.GetEnumerator();
        foreach (var eventMessage in eventMessages)
        {
            var expectedTemplate = $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}";

            Assert.True(capturedCalls.MoveNext());
            var call = capturedCalls.Current;
            Assert.Equal(expectedTemplate, call.RenderedTemplate);

            Assert.True(capturedCalls.MoveNext());
            call = capturedCalls.Current;
            Assert.Equal(expectedTemplate, call.RenderedTemplate);
        }
    }

    private class TestIntegrationEventHandlerBase : IntegrationEventHandlerBase
    {
        public TestIntegrationEventHandlerBase(IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationIntegrationConfigurationRepository configurationRepository)
            : base(userRepository, organizationRepository, configurationRepository)
        { }

        public List<(JsonObject MergedConfiguration, string RenderedTemplate)> CapturedCalls { get; } = new();

        protected override IntegrationType GetIntegrationType() => IntegrationType.Webhook;

        protected override Task ProcessEventIntegrationAsync(JsonObject mergedConfiguration, string renderedTemplate)
        {
            CapturedCalls.Add((mergedConfiguration, renderedTemplate));
            return Task.CompletedTask;
        }
    }
}
