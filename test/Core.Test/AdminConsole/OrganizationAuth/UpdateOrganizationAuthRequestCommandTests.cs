using Bit.Core.AdminConsole.OrganizationAuth;
using Bit.Core.AdminConsole.OrganizationAuth.Models;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationAuth;

[SutProviderCustomize]
public class UpdateOrganizationAuthRequestCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateOrgAuthRequest_Approved_SendEmail_Success(
        DateTime responseDate, string email, DeviceType deviceType, string deviceIdentifier,
        string requestIpAddress, Guid requestId, Guid userId, bool requestApproved,
        string encryptedUserKey, SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider)
    {
        var expectedDeviceTypeAndIdentifier = $"{deviceType} - {deviceIdentifier}";

        sutProvider.GetDependency<IAuthRequestService>()
            .UpdateAuthRequestAsync(requestId, userId,
                Arg.Is<AuthRequestUpdateRequestModel>(x =>
                    x.RequestApproved == requestApproved && x.Key == encryptedUserKey))
            .Returns(new AuthRequest()
            {
                UserId = userId,
                Approved = true,
                ResponseDate = responseDate,
                RequestDeviceType = deviceType,
                RequestDeviceIdentifier = deviceIdentifier,
                RequestIpAddress = requestIpAddress,
            });

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(userId)
            .Returns(new User()
            {
                Email = email
            });

        await sutProvider.Sut.UpdateAsync(requestId, userId, requestApproved, encryptedUserKey);

        await sutProvider.GetDependency<IUserRepository>().Received(1).GetByIdAsync(userId);
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendTrustedDeviceAdminApprovalEmailAsync(email, responseDate, requestIpAddress, expectedDeviceTypeAndIdentifier);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrgAuthRequest_Denied_NonExecutes(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider, Guid requestId, Guid userId,
        bool requestApproved, string encryptedUserKey)
    {
        sutProvider.GetDependency<IAuthRequestService>()
            .UpdateAuthRequestAsync(requestId, userId,
                Arg.Is<AuthRequestUpdateRequestModel>(x =>
                    x.RequestApproved == requestApproved && x.Key == encryptedUserKey))
            .Returns(new AuthRequest() { Approved = false });

        await sutProvider.Sut.UpdateAsync(requestId, userId, requestApproved, encryptedUserKey);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().GetByIdAsync(userId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendTrustedDeviceAdminApprovalEmailAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(),
                Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_BatchUpdate_AuthRequestForOrganizationNotFound_DoesNotExecute(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        List<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration)
    {
        sutProvider.GetDependency<IAuthRequestRepository>().GetManyAdminApprovalRequestsByManyIdsAsync(
            configuration.OrganizationId,
            updates.Select(ar => ar.Id)
        ).ReturnsForAnyArgs((ICollection<OrganizationAdminAuthRequest>)null);

        await sutProvider.Sut.UpdateAsync(configuration.OrganizationId, updates);
        await sutProvider.GetDependency<IAuthRequestRepository>().DidNotReceiveWithAnyArgs().UpdateManyAsync(Arg.Any<IEnumerable<OrganizationAdminAuthRequest>>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushAuthRequestResponseAsync(Arg.Any<AuthRequest>());
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendTrustedDeviceAdminApprovalEmailAsync(
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<string>(),
            Arg.Any<string>()
        );
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogOrganizationUserEventsAsync(
            Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>()
        );
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_BatchUpdate_ValidRequest_SavesAndFiresAllEvents(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        List<OrganizationAuthRequestUpdate> updates,
        List<OrganizationAdminAuthRequest> unprocessedAuthRequests,
        AuthRequestUpdateProcessorConfiguration configuration,
        List<OrganizationUser> organizationUsers,
        List<User> users
    )
    {
        // For this command to work we need the following from external
        // classes:
        // 1. A configured expiration timespan for organization auth requests
        // 2. Some unresponded to auth requests that match the ids provided
        // 3. A valid user to send emails to
        // 4. A valid organization user to log events for

        for (int i = 0; i < updates.Count(); i++)
        {
            unprocessedAuthRequests[i] = UnrespondAndEnsureValid(unprocessedAuthRequests[i], configuration.OrganizationId);
            updates[i].Approved = true;
            updates[i].Key = "key";
            unprocessedAuthRequests[i].Id = updates[i].Id;
            unprocessedAuthRequests[i].RequestDeviceType = DeviceType.iOS;
            unprocessedAuthRequests[i].OrganizationUserId = organizationUsers[i].Id;
            organizationUsers[i].OrganizationId = configuration.OrganizationId;
            users[i].Id = unprocessedAuthRequests[i].UserId;
            organizationUsers[i].UserId = unprocessedAuthRequests[i].UserId;

            sutProvider.GetDependency<IUserRepository>().GetByIdAsync(Arg.Is(users[i].Id)).Returns(users[i]);
        };

        sutProvider.GetDependency<IGlobalSettings>().PasswordlessAuth.AdminRequestExpiration.Returns(TimeSpan.FromDays(7));

        sutProvider.GetDependency<IAuthRequestRepository>().GetManyAdminApprovalRequestsByManyIdsAsync(
            configuration.OrganizationId,
            updates.Select(ar => ar.Id)
        ).ReturnsForAnyArgs(unprocessedAuthRequests);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Is<IEnumerable<Guid>>(
            list => list.All(x => organizationUsers.Select(y => y.Id).Contains(x)))).Returns(organizationUsers);

        // Call the SUT
        await sutProvider.Sut.UpdateAsync(configuration.OrganizationId, updates);

        // Assert that because we passed in good data we call a save
        // operation and raise all events
        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received()
            .UpdateManyAsync(
                Arg.Is<IEnumerable<OrganizationAdminAuthRequest>>(list =>
                    list.Any() &&
                    list.All(x =>
                        x.Approved.Value &&
                        x.Key == "key" &&
                        x.ResponseDate != null &&
                        unprocessedAuthRequests.Select(y => y.Id).Contains(x.Id))));

        foreach (var authRequest in unprocessedAuthRequests)
        {
            await sutProvider.GetDependency<IPushNotificationService>().Received()
                .PushAuthRequestResponseAsync(Arg.Is<OrganizationAdminAuthRequest>
                        (ar => ar.Id == authRequest.Id && ar.Approved == true && ar.Key == "key"));

            await sutProvider.GetDependency<IMailService>().Received().SendTrustedDeviceAdminApprovalEmailAsync(
                users.FirstOrDefault(x => x.Id == authRequest.UserId).Email,
                Arg.Any<DateTime>(),
                authRequest.RequestIpAddress,
                $"iOS - {authRequest.RequestDeviceIdentifier}"
            );
        }

        await sutProvider.GetDependency<IEventService>().Received().LogOrganizationUserEventsAsync(
            Arg.Is<IEnumerable<(OrganizationUser o, EventType e, DateTime? d)>>(list =>
                list.Any() && list.All(x => organizationUsers.Any(y => y.Id == x.o.Id) && x.e == EventType.OrganizationUser_ApprovedAuthRequest)
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_BatchUpdate_AuthRequestIsDenied_DoesNotLeakRejection(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        List<OrganizationAuthRequestUpdate> updates,
        OrganizationAdminAuthRequest unprocessedAuthRequest,
        AuthRequestUpdateProcessorConfiguration configuration,
        User user
    )
    {
        // For this command to work we need the following from external
        // classes:
        // 1. A configured expiration timespan for organization auth requests
        // 2. Some unresponded to auth requests that match the ids provided
        // 3. A valid user to send emails to

        var unprocessedAuthRequests = new List<OrganizationAdminAuthRequest>();
        unprocessedAuthRequest = UnrespondAndEnsureValid(unprocessedAuthRequest, configuration.OrganizationId);
        foreach (var update in updates)
        {
            update.Approved = false;
            unprocessedAuthRequest.Id = update.Id;
            unprocessedAuthRequests.Add(unprocessedAuthRequest);
        };

        sutProvider.GetDependency<IGlobalSettings>().PasswordlessAuth.AdminRequestExpiration.Returns(TimeSpan.FromDays(7));

        sutProvider.GetDependency<IAuthRequestRepository>().GetManyAdminApprovalRequestsByManyIdsAsync(
            configuration.OrganizationId,
            updates.Select(ar => ar.Id)
        ).ReturnsForAnyArgs(unprocessedAuthRequests);

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(user);

        // Call the SUT
        await sutProvider.Sut.UpdateAsync(configuration.OrganizationId, updates);

        // Assert that because we passed in good data we call a save
        // operation and raise all events
        await sutProvider.GetDependency<IAuthRequestRepository>().ReceivedWithAnyArgs().UpdateManyAsync(Arg.Any<IEnumerable<OrganizationAdminAuthRequest>>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushAuthRequestResponseAsync(Arg.Any<AuthRequest>());
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendTrustedDeviceAdminApprovalEmailAsync(
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<string>(),
            Arg.Any<string>()
        );
        await sutProvider.GetDependency<IEventService>().ReceivedWithAnyArgs().LogOrganizationUserEventsAsync(
            Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>()
        );
    }

    private T UnrespondAndEnsureValid<T>(T authRequest, Guid organizationId) where T : AuthRequest
    {
        authRequest.OrganizationId = organizationId;
        authRequest.Key = null;
        authRequest.Approved = null;
        authRequest.ResponseDate = null;
        authRequest.AuthenticationDate = null;
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        return authRequest;
    }
}
