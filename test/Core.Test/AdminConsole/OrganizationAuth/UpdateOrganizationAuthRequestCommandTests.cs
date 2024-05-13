using Bit.Core.AdminConsole.OrganizationAuth;
using Bit.Core.AdminConsole.OrganizationAuth.Models;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
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
    public async Task UpdateOrgAuthRequest_Approved_UserNotFound(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider, Guid requestId, Guid userId,
        bool requestApproved, string encryptedUserKey)
    {
        sutProvider.GetDependency<IAuthRequestService>()
            .UpdateAuthRequestAsync(requestId, userId,
                Arg.Is<AuthRequestUpdateRequestModel>(x =>
                    x.RequestApproved == requestApproved && x.Key == encryptedUserKey))
            .Returns(new AuthRequest() { Approved = true, });

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(userId)
            .ReturnsNull();

        await sutProvider.Sut.UpdateAsync(requestId, userId, requestApproved, encryptedUserKey);

        await sutProvider.GetDependency<IUserRepository>().Received(1).GetByIdAsync(userId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendTrustedDeviceAdminApprovalEmailAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(),
                Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateManyOrganizationAuthRequestsInTheDatabase_NullInput_Returns(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider)
    {
        await sutProvider.Sut.UpdateManyOrganizationAuthRequestsInTheDatabase(null);
        await sutProvider.GetDependency<IAuthRequestRepository>().DidNotReceive().UpdateManyAsync(Arg.Any<IEnumerable<AuthRequest>>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateManyOrganizationAuthRequestsInTheDatabase_EmptyInput_Returns(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider)
    {
        await sutProvider.Sut.UpdateManyOrganizationAuthRequestsInTheDatabase(new List<OrganizationAdminAuthRequest>());
        await sutProvider.GetDependency<IAuthRequestRepository>().DidNotReceive().UpdateManyAsync(Arg.Any<IEnumerable<AuthRequest>>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateManyOrganizationAuthRequestsInTheDatabase_ValidInput_CallsRepository(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        IEnumerable<OrganizationAdminAuthRequest> authRequests
    )
    {
        await sutProvider.Sut.UpdateManyOrganizationAuthRequestsInTheDatabase(authRequests);
        await sutProvider.GetDependency<IAuthRequestRepository>().Received(1).UpdateManyAsync(authRequests);
    }

    [Theory]
    [BitAutoData]
    public async Task FetchManyOrganizationAuthRequestsFromTheDatabase_NullInput_Returns(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        Guid organizationId
    )
    {
        await sutProvider.Sut.FetchManyOrganizationAuthRequestsFromTheDatabase(organizationId, null);
        await sutProvider.GetDependency<IAuthRequestRepository>().DidNotReceive().GetManyAdminApprovalRequestsByManyIdsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [BitAutoData]
    public async Task FetchManyOrganizationAuthRequestsFromTheDatabase_EmptyInput_Returns(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        Guid organizationId
    )
    {
        await sutProvider.Sut.FetchManyOrganizationAuthRequestsFromTheDatabase(organizationId, new List<Guid>());
        await sutProvider.GetDependency<IAuthRequestRepository>().DidNotReceive().GetManyAdminApprovalRequestsByManyIdsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [BitAutoData]
    public async Task FetchManyOrganizationAuthRequestsFromTheDatabase_ValidInput_CallsRepository(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        Guid organizationId,
        IEnumerable<Guid> authRequestIds
    )
    {
        await sutProvider.Sut.FetchManyOrganizationAuthRequestsFromTheDatabase(organizationId, authRequestIds);
        await sutProvider.GetDependency<IAuthRequestRepository>().Received(1).GetManyAdminApprovalRequestsByManyIdsAsync(organizationId, authRequestIds);
    }

    [Theory]
    [BitAutoData]
    public void ProcessManyAuthRequests_NullInput_ReturnsEmptyList(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        Guid organizationId
    )
    {
        var sutResponse = sutProvider.Sut.ProcessManyAuthRequests<OrganizationAdminAuthRequest>(null, null, organizationId);
        Assert.Equal(sutResponse, new List<OrganizationAdminAuthRequest>());
    }

    [Theory]
    [BitAutoData]
    public void ProcessManyAuthRequests_NullRequestsInput_ReturnsEmptyList(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        IEnumerable<OrganizationAuthRequestUpdate> updates,
        Guid organizationId
    )
    {
        var sutResponse = sutProvider.Sut.ProcessManyAuthRequests<OrganizationAdminAuthRequest>(null, updates, organizationId);
        Assert.Equal(sutResponse, new List<OrganizationAdminAuthRequest>());
    }

    [Theory]
    [BitAutoData]
    public void ProcessManyAuthRequests_NullUpdatesInput_ReturnsEmptyList(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        IEnumerable<OrganizationAdminAuthRequest> authRequestRecords,
        Guid organizationId
    )
    {
        var sutResponse = sutProvider.Sut.ProcessManyAuthRequests<OrganizationAdminAuthRequest>(authRequestRecords, null, organizationId);
        Assert.Equal(sutResponse, new List<OrganizationAdminAuthRequest>());
    }

    [Theory]
    [BitAutoData]
    public void ProcessManyAuthRequests_ValidInput_ReturnsPopulatedList(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        List<OrganizationAdminAuthRequest> authRequestRecords,
        List<OrganizationAuthRequestUpdate> updates,
        Guid organizationId,
        string key
    )
    {
        authRequestRecords[0] = UnrespondToAuthRequest(authRequestRecords[0]);
        authRequestRecords[0].CreationDate = DateTime.UtcNow.AddDays(-1);
        authRequestRecords[0].OrganizationId = organizationId;
        updates[0].Approved = true;
        updates[0].Key = key;
        updates[0].Id = authRequestRecords[0].Id;
        sutProvider.Sut.FetchRequestExpirationTimespan().Returns(DateTime.UtcNow.AddDays(5) - authRequestRecords[0].CreationDate);
        var sutResponse = sutProvider.Sut.ProcessManyAuthRequests(authRequestRecords, updates, organizationId);
        Assert.NotEmpty(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void ApproveAuthRequest_NoKey_LogsAndReturnsEarly(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequestToApprove
    )
    {
        var processedAuthRequest = sutProvider.Sut.ApproveAuthRequest(authRequestToApprove, null);
        sutProvider.GetDependency<ILogger<UpdateOrganizationAuthRequestCommand>>().ReceivedWithAnyArgs(1).LogError("error message");
        Assert.Equal(authRequestToApprove, processedAuthRequest);
    }

    [Theory]
    [BitAutoData]
    public void ApproveAuthRequest_ValidInput_UpdatesNecassaryProperties(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequestToApprove,
        string Key
    )
    {
        authRequestToApprove = UnrespondToAuthRequest(authRequestToApprove);
        var sutResponse = sutProvider.Sut.ApproveAuthRequest(authRequestToApprove, Key);
        Assert.Equal(sutResponse.Key, Key);
        Assert.True(sutResponse.Approved);
        Assert.NotNull(sutResponse.ResponseDate);
    }

    [Theory]
    [BitAutoData]
    public void DenyAuthRequest_ValidInput_UpdatesNecassaryProperties(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequestToDeny
    )
    {
        authRequestToDeny = UnrespondToAuthRequest(authRequestToDeny);
        var sutResponse = sutProvider.Sut.DenyAuthRequest(authRequestToDeny);
        Assert.False(sutResponse.Approved);
        Assert.NotNull(sutResponse.ResponseDate);
        Assert.Null(sutResponse.Key);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestSpent_NullInput_RequestIsSpent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider
    )
    {
        var sutResponse = sutProvider.Sut.IsAuthRequestSpent((OrganizationAdminAuthRequest)null);
        Assert.True(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestSpent_AuthRequestIsApproved_RequestIsSpent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        string Key
    )
    {
        authRequest = sutProvider.Sut.ApproveAuthRequest(authRequest, Key);
        var sutResponse = sutProvider.Sut.IsAuthRequestSpent(authRequest);
        Assert.True(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestSpent_AuthRequestIsDenied_RequestIsSpent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest = sutProvider.Sut.DenyAuthRequest(authRequest);
        var sutResponse = sutProvider.Sut.IsAuthRequestSpent(authRequest);
        Assert.True(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestSpent_AuthRequestIsUnresolved_RequestIsNotSpent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest = UnrespondToAuthRequest(authRequest);
        var sutResponse = sutProvider.Sut.IsAuthRequestSpent(authRequest);
        Assert.False(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestExpired_NullInput_IsExpired(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider
    )
    {
        var sutResponse = sutProvider.Sut.IsAuthRequestExpired((OrganizationAdminAuthRequest)null);
        Assert.True(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestExpired_RequestIsExpired_IsExpired(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest.CreationDate = DateTime.UtcNow.AddDays(-1);
        sutProvider.Sut.FetchRequestExpirationTimespan().Returns(TimeSpan.MinValue);
        var sutResponse = sutProvider.Sut.IsAuthRequestExpired(authRequest);
        Assert.True(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestExpired_RequestIsNotExpired_IsNotExpired(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest.CreationDate = DateTime.UtcNow.AddDays(-1);
        sutProvider.Sut.FetchRequestExpirationTimespan().Returns(DateTime.UtcNow.AddDays(5) - authRequest.CreationDate);
        var sutResponse = sutProvider.Sut.IsAuthRequestExpired(authRequest);
        Assert.False(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestBeingUpdated_NoUpdate_False(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        IEnumerable<OrganizationAuthRequestUpdate> authRequestUpdates
    )
    {
        var checkForMatchingIds = true;
        while (checkForMatchingIds)
        {
            checkForMatchingIds = false;
            foreach (var update in authRequestUpdates)
            {
                if (authRequest.Id == update.Id)
                {
                    authRequest.Id = new Guid();
                    checkForMatchingIds = true;
                }
            }
        }
        var sutResponse = sutProvider.Sut.IsAuthRequestBeingUpdated(authRequest, authRequestUpdates);
        Assert.False(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestBeingUpdated_UpdateFound_True(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        List<OrganizationAuthRequestUpdate> authRequestUpdates
    )
    {
        authRequest.Id = authRequestUpdates[0].Id;
        var sutResponse = sutProvider.Sut.IsAuthRequestBeingUpdated(authRequest, authRequestUpdates);
        Assert.True(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestForTheCorrectOrganization_OrganizationIdDoesNotMatch_False(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        Guid organizationId
    )
    {
        while (true)
        {
            if (authRequest.OrganizationId != organizationId)
            {
                break;
            }
            organizationId = new Guid();
        }
        var sutResponse = sutProvider.Sut.IsAuthRequestForTheCorrectOrganization(authRequest, organizationId);
        Assert.False(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public void IsAuthRequestForTheCorrectOrganization_OrganizationIdMatches_True(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        Guid organizationId
    )
    {
        authRequest.OrganizationId = organizationId;
        var sutResponse = sutProvider.Sut.IsAuthRequestForTheCorrectOrganization(authRequest, organizationId);
        Assert.True(sutResponse);
    }

    [Theory]
    [BitAutoData]
    public async Task PushManyAuthRequestNotifications_NullInput_NoNotificationsSent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider
    )
    {
        var pushedNotifications = await sutProvider.Sut.PushManyAuthRequestNotifications((List<OrganizationAdminAuthRequest>)null);
        Assert.False(pushedNotifications);
    }

    [Theory]
    [BitAutoData]
    public async Task PushManyAuthRequestNotifications_HasInput_SendsNotifications(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        IEnumerable<OrganizationAdminAuthRequest> authRequests
    )
    {
        var pushedNotifications = await sutProvider.Sut.PushManyAuthRequestNotifications(authRequests);
        Assert.True(pushedNotifications);
    }

    [Theory]
    [BitAutoData]
    public async Task PushAuthRequestNotification_RequestIsNotApproved_NoNotificationSent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest = UnrespondToAuthRequest(authRequest);
        authRequest = sutProvider.Sut.DenyAuthRequest(authRequest);
        await sutProvider.Sut.PushAuthRequestNotification(authRequest);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive().PushAuthRequestResponseAsync(Arg.Any<OrganizationAdminAuthRequest>());
    }

    [Theory]
    [BitAutoData]
    public async Task PushAuthRequestNotification_RequestIsApproved_NotificationSent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        string Key
    )
    {
        authRequest = UnrespondToAuthRequest(authRequest);
        authRequest = sutProvider.Sut.ApproveAuthRequest(authRequest, Key);
        await sutProvider.Sut.PushAuthRequestNotification(authRequest);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushAuthRequestResponseAsync(authRequest);
    }

    [Theory]
    [BitAutoData]
    public async Task PushManyTrustedDeviceEmails_NullInput_NoEmailsSent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider
    )
    {
        var pushedEmails = await sutProvider.Sut.PushManyTrustedDeviceEmails((List<OrganizationAdminAuthRequest>)null);
        Assert.False(pushedEmails);
    }

    [Theory]
    [BitAutoData]
    public async Task PushManyTrustedDeviceEmails_HasInput_SendsEmails(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        IEnumerable<OrganizationAdminAuthRequest> authRequests
    )
    {
        var pushedEmails = await sutProvider.Sut.PushManyAuthRequestNotifications(authRequests);
        Assert.True(pushedEmails);
    }

    [Theory]
    [BitAutoData]
    public async Task PushTrustedDeviceEmail_RequestIsNotApproved_NoEmailSent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest = UnrespondToAuthRequest(authRequest);
        authRequest = sutProvider.Sut.DenyAuthRequest(authRequest);
        await sutProvider.Sut.PushTrustedDeviceEmail(authRequest);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendTrustedDeviceAdminApprovalEmailAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task PushTrustedDeviceEmail_RequestIsApprovedButUserIsNotFound_NoEmailSent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        string Key
    )
    {
        authRequest = UnrespondToAuthRequest(authRequest);
        authRequest = sutProvider.Sut.ApproveAuthRequest(authRequest, Key);
        sutProvider.Sut.FetchUserFromTheDatabase(authRequest.UserId).Returns((User)null);
        await sutProvider.Sut.PushAuthRequestNotification(authRequest);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendTrustedDeviceAdminApprovalEmailAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task PushTrustedDeviceEmail_RequestIsApprovedAndUserIsFound_EmailSent(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        string Key,
        User user
    )
    {
        authRequest = UnrespondToAuthRequest(authRequest);
        authRequest = sutProvider.Sut.ApproveAuthRequest(authRequest, Key);
        sutProvider.Sut.FetchUserFromTheDatabase(authRequest.UserId).Returns(user);
        await sutProvider.Sut.PushTrustedDeviceEmail(authRequest);
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendTrustedDeviceAdminApprovalEmailAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task PushAuthRequestEventLogs_NullInput_NoLogsPushed(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider
    )
    {
        var pushedEventLogs = await sutProvider.Sut.PushManyAuthRequestEventLogs((List<OrganizationAdminAuthRequest>)null);
        Assert.False(pushedEventLogs);
    }

    [Theory]
    [BitAutoData]
    public async Task PushAuthRequestEventLogs_HasInput_PushesLogs(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        IEnumerable<OrganizationAdminAuthRequest> authRequests
    )
    {
        var pushedEventLogs = await sutProvider.Sut.PushManyAuthRequestEventLogs(authRequests);
        Assert.True(pushedEventLogs);
    }

    [Theory]
    [BitAutoData]
    public void CalculateOrganizationAuthRequestProcessingEventLogType_AuthRequestIsApproved_ReturnsCorrectType(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        string Key
    )
    {
        authRequest = UnrespondToAuthRequest(authRequest);
        authRequest = sutProvider.Sut.ApproveAuthRequest(authRequest, Key);
        var type = sutProvider.Sut.CalculateOrganizationAuthRequestProcessingEventLogType(authRequest);
        Assert.Equal(EventType.OrganizationUser_ApprovedAuthRequest, type);
    }

    [Theory]
    [BitAutoData]
    public void CalculateOrganizationAuthRequestProcessingEventLogType_AuthRequestIsDenied_ReturnsCorrectType(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest = UnrespondToAuthRequest(authRequest);
        authRequest = sutProvider.Sut.DenyAuthRequest(authRequest);
        var type = sutProvider.Sut.CalculateOrganizationAuthRequestProcessingEventLogType(authRequest);
        Assert.Equal(EventType.OrganizationUser_RejectedAuthRequest, type);
    }

    [Theory]
    [BitAutoData]
    public async Task PushAuthRequestEventLog_OrganizationUserNotFound_LogsAndReturnsEarly(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        sutProvider.Sut.FetchOrganizationUserFromTheDatabase(authRequest).Returns((OrganizationUser)null);
        await sutProvider.Sut.PushTrustedDeviceEmail(authRequest);
        sutProvider.GetDependency<ILogger<UpdateOrganizationAuthRequestCommand>>().ReceivedWithAnyArgs(1).LogError("error message");
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceive().LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());
    }

    [Theory]
    [BitAutoData]
    public async Task PushAuthRequestEventLog_ValidInput_PushesEventLog(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        OrganizationUser organizationUser
    )
    {
        sutProvider.Sut.FetchOrganizationUserFromTheDatabase(authRequest).Returns(organizationUser);
        await sutProvider.Sut.PushAuthRequestEventLog(authRequest);
        await sutProvider.GetDependency<IEventService>()
            .Received(1).LogOrganizationUserEventAsync(organizationUser, Arg.Any<EventType>());
    }

    [Theory]
    [BitAutoData]
    public void InferDeviceTypeDisplayName_ValidDisplayAttribute_IsHandled(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest.RequestDeviceType = DeviceType.ChromeBrowser;
        var displayName = sutProvider.Sut.InferDeviceTypeDisplayName(authRequest);
        Assert.Equal("Chrome", displayName);
    }

    [Theory]
    [BitAutoData]
    public void BuildDeviceTypeAndIdentifierDisplayString_NullInput_IsHandled(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider
    )
    {
        var deviceTypeAndIdentifierDisplayString = sutProvider.Sut.BuildDeviceTypeAndIdentifierDisplayString((OrganizationAdminAuthRequest)null);
        Assert.Equal("Unknown Device", deviceTypeAndIdentifierDisplayString);
    }

    [Theory]
    [BitAutoData]
    public void BuildDeviceTypeAndIdentifierDisplayString_ValidDeviceTypeWithNoIdentifier_IsHandled(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest.RequestDeviceType = DeviceType.FirefoxExtension;
        authRequest.RequestDeviceIdentifier = null;
        var deviceTypeAndIdentifierDisplayString = sutProvider.Sut.BuildDeviceTypeAndIdentifierDisplayString(authRequest);
        Assert.Equal("Firefox Extension", deviceTypeAndIdentifierDisplayString);
    }

    [Theory]
    [BitAutoData]
    public void BuildDeviceTypeAndIdentifierDisplayString_ValidDeviceTypeAndIdentifier_IsHandled(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest.RequestDeviceType = DeviceType.iOS;
        var deviceTypeAndIdentifierDisplayString = sutProvider.Sut.BuildDeviceTypeAndIdentifierDisplayString(authRequest);
        Assert.Equal(deviceTypeAndIdentifierDisplayString, $"iOS - {authRequest.RequestDeviceIdentifier}");
    }

    [Theory]
    [BitAutoData]
    public async Task FetchOrganizationUserFromTheDatabase_OrganizationIdIsNull_ReturnsEarly(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest
    )
    {
        authRequest.OrganizationId = null;
        var response = await sutProvider.Sut.FetchOrganizationUserFromTheDatabase(authRequest);
        Assert.Null(response);
    }

    [Theory]
    [BitAutoData]
    public async Task FetchOrganizationUserFromTheDatabase_ValidInput_Works(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        OrganizationAdminAuthRequest authRequest,
        Guid organizationId,
        Guid userId
    )
    {
        authRequest.OrganizationId = organizationId;
        authRequest.UserId = userId;
        var response = await sutProvider.Sut.FetchOrganizationUserFromTheDatabase(authRequest);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetByOrganizationAsync(organizationId, userId);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ValidRequest_DoesNotThrow(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider,
        IEnumerable<OrganizationAuthRequestUpdate> authRequestUpdates,
        OrganizationAdminAuthRequest existingAuthRequest,
        Guid organizationId
    )
    {
        existingAuthRequest = UnrespondToAuthRequest(existingAuthRequest);
        var updatedAuthRequests = authRequestUpdates.Select(aru =>
            {
                existingAuthRequest.Id = aru.Id;
                existingAuthRequest.OrganizationId = organizationId;
                return existingAuthRequest;
            }).ToList();
        sutProvider.Sut.FetchManyOrganizationAuthRequestsFromTheDatabase(organizationId, authRequestUpdates.Select(x => x.Id)).Returns(updatedAuthRequests);
        await sutProvider.Sut.UpdateAsync(organizationId, authRequestUpdates);
    }

    private T UnrespondToAuthRequest<T>(T authRequestToUnrespondTo) where T : AuthRequest
    {
        authRequestToUnrespondTo.Key = null;
        authRequestToUnrespondTo.Approved = null;
        authRequestToUnrespondTo.ResponseDate = null;
        authRequestToUnrespondTo.AuthenticationDate = null;
        return authRequestToUnrespondTo;
    }
}
