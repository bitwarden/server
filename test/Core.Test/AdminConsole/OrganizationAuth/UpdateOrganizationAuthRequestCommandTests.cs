using Bit.Core.AdminConsole.OrganizationAuth;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
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
}
