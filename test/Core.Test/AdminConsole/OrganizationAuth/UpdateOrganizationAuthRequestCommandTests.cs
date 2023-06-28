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
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationAuth;

[SutProviderCustomize]
public class UpdateOrganizationAuthRequestCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateOrgAuthRequest_Approved_SendEmail_Success(SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider, Guid requestId, Guid userId, bool requestApproved, string encryptedUserKey)
    {
        var responseDate = DateTime.UtcNow;
        const string email = "email";
        const DeviceType deviceType = DeviceType.ChromeBrowser;
        const string deviceIdentifier = "Intel Mac OSX 10_15_7";
        const string deviceTypeIdentifier = "Chrome - Intel Mac OSX 10_15_7";
        const string requestIpAddress = "127.0.0.1";

        sutProvider.GetDependency<IAuthRequestService>()
            .UpdateAuthRequestAsync(requestId, userId, Arg.Is<AuthRequestUpdateRequestModel>(x => x.RequestApproved == requestApproved && x.Key == encryptedUserKey))
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

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendTrustedDeviceAdminApprovalEmailAsync(email, responseDate, requestIpAddress, deviceTypeIdentifier);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrgAuthRequest_Denied_NonExecutes(
        SutProvider<UpdateOrganizationAuthRequestCommand> sutProvider, Guid requestId, Guid userId,
        bool requestApproved, string encryptedUserKey)
    {
        sutProvider.GetDependency<IAuthRequestService>()
            .UpdateAuthRequestAsync(requestId, userId, Arg.Is<AuthRequestUpdateRequestModel>(x => x.RequestApproved == requestApproved && x.Key == encryptedUserKey))
            .Returns(new AuthRequest()
            {
                Approved = false
            });

        await sutProvider.Sut.UpdateAsync(requestId, userId, requestApproved, encryptedUserKey);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().GetByIdAsync(userId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive().SendTrustedDeviceAdminApprovalEmailAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
