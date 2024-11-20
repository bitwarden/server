#nullable enable
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.NotificationHub;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Azure.NotificationHubs;
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
    public async void CreateOrUpdateRegistrationAsync_PushTokenNullOrEmpty_InstallationNotCreated(string? pushToken,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId, Guid userId, Guid identifier)
    {
        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifier.ToString(), DeviceType.Android);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(0)
            .ClientFor(deviceId);
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async void CreateOrUpdateRegistrationAsync_DeviceTypeAndroid_InstallationCreated(bool identifierNull,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId, Guid userId, Guid? identifier)
    {
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifierNull ? null : identifier.ToString(), DeviceType.Android);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Platform == NotificationPlatform.FcmV1 &&
                installation.Tags.Count == (identifierNull ? 2 : 3) &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains("clientType:Mobile") &&
                (identifierNull || installation.Tags.Contains($"deviceIdentifier:{identifier}")) &&
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
                    identifierNull ? null : $"template:payload_deviceIdentifier:{identifier}"
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
                    identifierNull ? null : $"template:message_deviceIdentifier:{identifier}"
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
                    identifierNull ? null : $"template:badgeMessage_deviceIdentifier:{identifier}"
                })));
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async void CreateOrUpdateRegistrationAsync_DeviceTypeIOS_InstallationCreated(bool identifierNull,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId, Guid userId, Guid identifier)
    {
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifierNull ? null : identifier.ToString(), DeviceType.iOS);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Platform == NotificationPlatform.Apns &&
                installation.Tags.Count == (identifierNull ? 2 : 3) &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains("clientType:Mobile") &&
                (identifierNull || installation.Tags.Contains($"deviceIdentifier:{identifier}")) &&
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
                    identifierNull ? null : $"template:payload_deviceIdentifier:{identifier}"
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
                    identifierNull ? null : $"template:message_deviceIdentifier:{identifier}"
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
                    identifierNull ? null : $"template:badgeMessage_deviceIdentifier:{identifier}"
                })));
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async void CreateOrUpdateRegistrationAsync_DeviceTypeAndroidAmazon_InstallationCreated(bool identifierNull,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId, Guid userId, Guid identifier)
    {
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifierNull ? null : identifier.ToString(), DeviceType.AndroidAmazon);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Platform == NotificationPlatform.Adm &&
                installation.Tags.Count == (identifierNull ? 2 : 3) &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains("clientType:Mobile") &&
                (identifierNull || installation.Tags.Contains($"deviceIdentifier:{identifier}")) &&
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
                    identifierNull ? null : $"template:payload_deviceIdentifier:{identifier}"
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
                    identifierNull ? null : $"template:message_deviceIdentifier:{identifier}"
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
                    identifierNull ? null : $"template:badgeMessage_deviceIdentifier:{identifier}"
                })));
    }

    [Theory]
    [BitAutoData(DeviceType.ChromeBrowser)]
    [BitAutoData(DeviceType.ChromeExtension)]
    [BitAutoData(DeviceType.MacOsDesktop)]
    public async void CreateOrUpdateRegistrationAsync_DeviceTypeNotMobile_InstallationCreated(DeviceType deviceType,
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId, Guid userId, Guid identifier)
    {
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifier.ToString(), deviceType);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Tags.Count == 3 &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains($"clientType:{DeviceTypes.ToClientType(deviceType)}") &&
                installation.Tags.Contains($"deviceIdentifier:{identifier}") &&
                installation.Templates.Count == 0));
    }

    [Theory]
    [BitAutoData]
    public async void CreateOrUpdateRegistrationAsync_UserPartOfOrganization_InstallationCreated(
        SutProvider<NotificationHubPushRegistrationService> sutProvider, Guid deviceId, Guid userId, Guid identifier,
        OrganizationUserOrganizationDetails organizationDetails)
    {
        var notificationHubClient = Substitute.For<INotificationHubClient>();
        sutProvider.GetDependency<INotificationHubPool>().ClientFor(Arg.Any<Guid>()).Returns(notificationHubClient);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByUserAsync(userId, OrganizationUserStatusType.Confirmed)
            .Returns([organizationDetails]);

        var pushToken = "test push token";

        await sutProvider.Sut.CreateOrUpdateRegistrationAsync(pushToken, deviceId.ToString(), userId.ToString(),
            identifier.ToString(), DeviceType.ChromeBrowser);

        sutProvider.GetDependency<INotificationHubPool>()
            .Received(1)
            .ClientFor(deviceId);
        await notificationHubClient
            .Received(1)
            .CreateOrUpdateInstallationAsync(Arg.Is<Installation>(installation =>
                installation.InstallationId == deviceId.ToString() &&
                installation.PushChannel == pushToken &&
                installation.Tags.Count == 4 &&
                installation.Tags.Contains($"userId:{userId}") &&
                installation.Tags.Contains($"clientType:{DeviceTypes.ToClientType(DeviceType.ChromeBrowser)}") &&
                installation.Tags.Contains($"deviceIdentifier:{identifier}") &&
                installation.Tags.Contains($"organizationId:{organizationDetails.OrganizationId}")));
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
