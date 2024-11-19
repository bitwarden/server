#nullable enable
using Bit.Core.Enums;
using Bit.Core.NotificationHub;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationHub;

[SutProviderCustomize]
public class NotificationHubPushRegistrationServiceTests
{
    [Theory]
    [BitAutoData([null])]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task CreateOrUpdateRegistrationAsync_PushTokenNullOrEmpty_InstallationNotCreated(string? pushToken,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId, Guid userId,
        Guid identifier, Guid installationId, Guid organizationId)
    {
        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifier.ToString(), DeviceType.Android, installationId.ToString(), [organizationId.ToString()]);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(0)
            .ClientFor(deviceId);
    }

    [Theory]
    [BitAutoData(false, false, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, true, true)]
    [BitAutoData(true, false, false)]
    [BitAutoData(true, false, true)]
    [BitAutoData(true, true, false)]
    [BitAutoData(true, true, true)]
    public async Task CreateOrUpdateRegistrationAsync_DeviceTypeAndroid_InstallationCreated(bool identifierNull,
        bool installationIdNull, bool partOfOrganizationId,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId,
        Guid userId, Guid? identifier, Guid installationId, Guid organizationId)
    {
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.AnhFcmv1Migration).Returns(true);
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IFeatureService)).Returns(featureService);
        var serviceScope = Substitute.For<IServiceScope>();
        serviceScope.ServiceProvider.Returns(serviceProvider);
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        sutProvider.GetDependency<IServiceProvider>().GetService(typeof(IServiceScopeFactory))
            .Returns(serviceScopeFactory);
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifierNull ? null : identifier.ToString(), DeviceType.Android,
            installationIdNull ? null : installationId.ToString(),
            partOfOrganizationId ? [organizationId.ToString()] : []);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Platform == NotificationPlatform.FcmV1 &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains("clientType:Mobile") &&
                (identifierNull || installation.Tags.Contains($"deviceIdentifier:{identifier}")) &&
                (installationIdNull || installation.Tags.Contains($"installationId:{installationId}")) &&
                (!partOfOrganizationId || installation.Tags.Contains($"organizationId:{organizationId}")) &&
                installation.Templates.Count == 3));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:payload",
                "{\"message\":{\"data\":{\"type\":\"$(type)\",\"payload\":\"$(payload)\"}}}",
                new List<string?>
                {
                    "template:payload",
                    $"template:payload_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:payload_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:message",
                "{\"message\":{\"data\":{\"type\":\"$(type)\"},\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}}",
                new List<string?>
                {
                    "template:message",
                    $"template:message_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:message_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:badgeMessage",
                "{\"message\":{\"data\":{\"type\":\"$(type)\"},\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}}",
                new List<string?>
                {
                    "template:badgeMessage",
                    $"template:badgeMessage_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:badgeMessage_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
    }

    [Theory]
    [BitAutoData(false, false, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, true, true)]
    [BitAutoData(true, false, false)]
    [BitAutoData(true, false, true)]
    [BitAutoData(true, true, false)]
    [BitAutoData(true, true, true)]
    public async Task CreateOrUpdateRegistrationAsync_DeviceTypeIOS_InstallationCreated(bool identifierNull,
        bool installationIdNull, bool partOfOrganizationId,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId,
        Guid userId, Guid? identifier, Guid installationId, Guid organizationId)
    {
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifierNull ? null : identifier.ToString(), DeviceType.iOS,
            installationIdNull ? null : installationId.ToString(),
            partOfOrganizationId ? [organizationId.ToString()] : []);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Platform == NotificationPlatform.Apns &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains("clientType:Mobile") &&
                (identifierNull || installation.Tags.Contains($"deviceIdentifier:{identifier}")) &&
                (installationIdNull || installation.Tags.Contains($"installationId:{installationId}")) &&
                (!partOfOrganizationId || installation.Tags.Contains($"organizationId:{organizationId}")) &&
                installation.Templates.Count == 3));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:payload",
                "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"},\"aps\":{\"content-available\":1}}",
                new List<string?>
                {
                    "template:payload",
                    $"template:payload_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:payload_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:message",
                "{\"data\":{\"type\":\"#(type)\"},\"aps\":{\"alert\":\"$(message)\",\"badge\":null,\"content-available\":1}}",
                new List<string?>
                {
                    "template:message",
                    $"template:message_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:message_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:badgeMessage",
                "{\"data\":{\"type\":\"#(type)\"},\"aps\":{\"alert\":\"$(message)\",\"badge\":\"#(badge)\",\"content-available\":1}}",
                new List<string?>
                {
                    "template:badgeMessage",
                    $"template:badgeMessage_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:badgeMessage_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
    }

    [Theory]
    [BitAutoData(false, false, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, true, true)]
    [BitAutoData(true, false, false)]
    [BitAutoData(true, false, true)]
    [BitAutoData(true, true, false)]
    [BitAutoData(true, true, true)]
    public async Task CreateOrUpdateRegistrationAsync_DeviceTypeAndroidAmazon_InstallationCreated(bool identifierNull,
        bool installationIdNull, bool partOfOrganizationId,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId,
        Guid userId, Guid? identifier, Guid installationId, Guid organizationId)
    {
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifierNull ? null : identifier.ToString(), DeviceType.AndroidAmazon,
            installationIdNull ? null : installationId.ToString(),
            partOfOrganizationId ? [organizationId.ToString()] : []);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Platform == NotificationPlatform.Adm &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains("clientType:Mobile") &&
                (identifierNull || installation.Tags.Contains($"deviceIdentifier:{identifier}")) &&
                (installationIdNull || installation.Tags.Contains($"installationId:{installationId}")) &&
                (!partOfOrganizationId || installation.Tags.Contains($"organizationId:{organizationId}")) &&
                installation.Templates.Count == 3));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:payload",
                "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}",
                new List<string?>
                {
                    "template:payload",
                    $"template:payload_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:payload_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:message",
                "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}",
                new List<string?>
                {
                    "template:message",
                    $"template:message_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:message_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation => MatchingInstallationTemplate(
                installation.Templates, "template:badgeMessage",
                "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}",
                new List<string?>
                {
                    "template:badgeMessage",
                    $"template:badgeMessage_userId:{userId}",
                    "clientType:Mobile",
                    identifierNull ? null : $"template:badgeMessage_deviceIdentifier:{identifier}",
                    installationIdNull ? null : $"installationId:{installationId}",
                    partOfOrganizationId ? $"organizationId:{organizationId}" : null,
                })));
    }

    [Theory]
    [BitAutoData(DeviceType.ChromeBrowser)]
    [BitAutoData(DeviceType.ChromeExtension)]
    [BitAutoData(DeviceType.MacOsDesktop)]
    public async Task CreateOrUpdateRegistrationAsync_DeviceTypeNotMobile_InstallationCreated(DeviceType deviceType,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId, Guid userId, Guid identifier,
        Guid installationId, Guid organizationId)
    {
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifier.ToString(), deviceType, installationId.ToString(), [organizationId.ToString()]);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains($"clientType:{DeviceTypes.ToClientType(deviceType)}") &&
                installation.Tags.Contains($"deviceIdentifier:{identifier}") &&
                installation.Tags.Contains($"installationId:{installationId}") &&
                installation.Tags.Contains($"organizationId:{organizationId}") &&
                installation.Templates.Count == 0));
    }

    private static bool MatchingInstallationTemplate(IDictionary<string, InstallationTemplate> templates, string key,
        string body, List<string?> tags)
    {
        var tagsNoNulls = tags.FindAll(tag => tag != null);
        return templates.ContainsKey(key) && templates[key].Body == body &&
               templates[key].Tags.Count == tagsNoNulls.Count &&
               templates[key].Tags.All(tagsNoNulls.Contains);
    }
}
