using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.CurrentContextFixtures;
using Bit.Core.Test.Entities;
using Bit.Core.Test.Tools.AutoFixture.SendFixtures;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

[SutProviderCustomize]
[CurrentContextCustomize]
[UserSendCustomize]
public class SendServiceTests
{
    private void SaveSendAsync_Setup(SendType sendType, bool disableSendPolicyAppliesToUser,
        SutProvider<SendService> sutProvider, Send send)
    {
        send.Id = default;
        send.Type = sendType;

        sutProvider.GetDependency<IPolicyService>().AnyPoliciesApplicableToUserAsync(
            Arg.Any<Guid>(), PolicyType.DisableSend).Returns(disableSendPolicyAppliesToUser);
    }

    // Disable Send policy check

    [Theory]
    [BitAutoData(SendType.File)]
    [BitAutoData(SendType.Text)]
    public async Task SaveSendAsync_DisableSend_Applies_throws(SendType sendType,
        SutProvider<SendService> sutProvider, Send send)
    {
        SaveSendAsync_Setup(sendType, disableSendPolicyAppliesToUser: true, sutProvider, send);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveSendAsync(send));
    }

    [Theory]
    [BitAutoData(SendType.File)]
    [BitAutoData(SendType.Text)]
    public async Task SaveSendAsync_DisableSend_DoesntApply_success(SendType sendType,
        SutProvider<SendService> sutProvider, Send send)
    {
        SaveSendAsync_Setup(sendType, disableSendPolicyAppliesToUser: false, sutProvider, send);

        await sutProvider.Sut.SaveSendAsync(send);

        await sutProvider.GetDependency<ISendRepository>().Received(1).CreateAsync(send);
    }

    // Send Options Policy - Disable Hide Email check

    private void SaveSendAsync_HideEmail_Setup(bool disableHideEmailAppliesToUser,
        SutProvider<SendService> sutProvider, Send send, Policy policy)
    {
        send.HideEmail = true;

        var sendOptions = new SendOptionsPolicyData
        {
            DisableHideEmail = disableHideEmailAppliesToUser
        };
        policy.Data = JsonSerializer.Serialize(sendOptions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        sutProvider.GetDependency<IPolicyService>().GetPoliciesApplicableToUserAsync(
            Arg.Any<Guid>(), PolicyType.SendOptions).Returns(new List<OrganizationUserPolicyDetails>()
            {
                new() { PolicyType = policy.Type, PolicyData = policy.Data, OrganizationId = policy.OrganizationId, PolicyEnabled = policy.Enabled }
            });
    }

    [Theory]
    [BitAutoData(SendType.File)]
    [BitAutoData(SendType.Text)]
    public async Task SaveSendAsync_DisableHideEmail_Applies_throws(SendType sendType,
        SutProvider<SendService> sutProvider, Send send, Policy policy)
    {
        SaveSendAsync_Setup(sendType, false, sutProvider, send);
        SaveSendAsync_HideEmail_Setup(true, sutProvider, send, policy);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveSendAsync(send));
    }

    [Theory]
    [BitAutoData(SendType.File)]
    [BitAutoData(SendType.Text)]
    public async Task SaveSendAsync_DisableHideEmail_DoesntApply_success(SendType sendType,
        SutProvider<SendService> sutProvider, Send send, Policy policy)
    {
        SaveSendAsync_Setup(sendType, false, sutProvider, send);
        SaveSendAsync_HideEmail_Setup(false, sutProvider, send, policy);

        await sutProvider.Sut.SaveSendAsync(send);

        await sutProvider.GetDependency<ISendRepository>().Received(1).CreateAsync(send);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveSendAsync_ExistingSend_Updates(SutProvider<SendService> sutProvider,
        Send send)
    {
        send.Id = Guid.NewGuid();

        var now = DateTime.UtcNow;
        await sutProvider.Sut.SaveSendAsync(send);

        Assert.True(send.RevisionDate - now < TimeSpan.FromSeconds(1));

        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpsertAsync(send);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSendUpdateAsync(send);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_TextType_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        send.Type = SendType.Text;

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 0)
        );

        Assert.Contains("not of type \"file\"", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_EmptyFile_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        send.Type = SendType.File;

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 0)
        );

        Assert.Contains("no file data", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_UserCannotAccessPremium_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
        };

        send.UserId = user.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(false);

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 1)
        );

        Assert.Contains("must have premium", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_UserHasUnconfirmedEmail_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmailVerified = false,
        };

        send.UserId = user.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 1)
        );

        Assert.Contains("must confirm your email", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_UserCanAccessPremium_HasNoStorage_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmailVerified = true,
            Premium = true,
            MaxStorageGb = null,
            Storage = 0,
        };

        send.UserId = user.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 1)
        );

        Assert.Contains("not enough storage", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_UserCanAccessPremium_StorageFull_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmailVerified = true,
            Premium = true,
            MaxStorageGb = 2,
            Storage = 2 * UserTests.Multiplier,
        };

        send.UserId = user.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 1)
        );

        Assert.Contains("not enough storage", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_UserCanAccessPremium_IsNotPremium_IsSelfHosted_GiantFile_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmailVerified = true,
            Premium = false,
        };

        send.UserId = user.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        sutProvider.GetDependency<Settings.GlobalSettings>()
            .SelfHosted = true;

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 11000 * UserTests.Multiplier)
        );

        Assert.Contains("not enough storage", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_UserCanAccessPremium_IsNotPremium_IsNotSelfHosted_TwoGigabyteFile_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmailVerified = true,
            Premium = false,
        };

        send.UserId = user.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        sutProvider.GetDependency<Settings.GlobalSettings>()
            .SelfHosted = false;

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 2 * UserTests.Multiplier)
        );

        Assert.Contains("not enough storage", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_ThroughOrg_MaxStorageIsNull_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            MaxStorageGb = null,
        };

        send.UserId = null;
        send.OrganizationId = org.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(org.Id)
            .Returns(org);

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 1)
        );

        Assert.Contains("organization cannot use file sends", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_ThroughOrg_MaxStorageIsNull_TwoGBFile_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            MaxStorageGb = null,
        };

        send.UserId = null;
        send.OrganizationId = org.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(org.Id)
            .Returns(org);

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 1)
        );

        Assert.Contains("organization cannot use file sends", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_ThroughOrg_MaxStorageIsOneGB_TwoGBFile_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            MaxStorageGb = 1,
        };

        send.UserId = null;
        send.OrganizationId = org.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(org.Id)
            .Returns(org);

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, null, 2 * UserTests.Multiplier)
        );

        Assert.Contains("not enough storage", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_HasEnoughStorage_Success(SutProvider<SendService> sutProvider,
        Send send)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmailVerified = true,
            MaxStorageGb = 10,
        };

        var data = new SendFileData
        {

        };

        send.UserId = user.Id;
        send.Type = SendType.File;

        var testUrl = "https://test.com/";

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        sutProvider.GetDependency<ISendFileStorageService>()
            .GetSendFileUploadUrlAsync(send, Arg.Any<string>())
            .Returns(testUrl);

        var utcNow = DateTime.UtcNow;

        var url = await sutProvider.Sut.SaveFileSendAsync(send, data, 1 * UserTests.Multiplier);

        Assert.Equal(testUrl, url);
        Assert.True(send.RevisionDate - utcNow < TimeSpan.FromSeconds(1));

        await sutProvider.GetDependency<ISendFileStorageService>()
            .Received(1)
            .GetSendFileUploadUrlAsync(send, Arg.Any<string>());

        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpsertAsync(send);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSendUpdateAsync(send);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveFileSendAsync_HasEnoughStorage_SendFileThrows_CleansUp(SutProvider<SendService> sutProvider,
        Send send)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmailVerified = true,
            MaxStorageGb = 10,
        };

        var data = new SendFileData
        {

        };

        send.UserId = user.Id;
        send.Type = SendType.File;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        sutProvider.GetDependency<ISendFileStorageService>()
            .GetSendFileUploadUrlAsync(send, Arg.Any<string>())
            .Returns<string>(callInfo => throw new Exception("Problem"));

        var utcNow = DateTime.UtcNow;

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            sutProvider.Sut.SaveFileSendAsync(send, data, 1 * UserTests.Multiplier)
        );

        Assert.True(send.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.Equal("Problem", exception.Message);

        await sutProvider.GetDependency<ISendFileStorageService>()
            .Received(1)
            .GetSendFileUploadUrlAsync(send, Arg.Any<string>());

        await sutProvider.GetDependency<ISendRepository>()
            .Received(1)
            .UpsertAsync(send);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSendUpdateAsync(send);

        await sutProvider.GetDependency<ISendFileStorageService>()
            .Received(1)
            .DeleteFileAsync(send, Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateFileToExistingSendAsync_SendNull_ThrowsBadRequest(SutProvider<SendService> sutProvider)
    {

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UploadFileToExistingSendAsync(new MemoryStream(), null)
        );

        Assert.Contains("does not have file data", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateFileToExistingSendAsync_SendDataNull_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        send.Data = null;

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UploadFileToExistingSendAsync(new MemoryStream(), send)
        );

        Assert.Contains("does not have file data", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateFileToExistingSendAsync_NotFileType_ThrowsBadRequest(SutProvider<SendService> sutProvider,
        Send send)
    {
        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UploadFileToExistingSendAsync(new MemoryStream(), send)
        );

        Assert.Contains("not a file type send", badRequest.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateFileToExistingSendAsync_Success(SutProvider<SendService> sutProvider,
        Send send)
    {
        var fileContents = "Test file content";

        var sendFileData = new SendFileData
        {
            Id = "TEST",
            Size = fileContents.Length,
            Validated = false,
        };

        send.Type = SendType.File;
        send.Data = JsonSerializer.Serialize(sendFileData);

        sutProvider.GetDependency<ISendFileStorageService>()
            .ValidateFileAsync(send, sendFileData.Id, sendFileData.Size, Arg.Any<long>())
            .Returns((true, sendFileData.Size));

        await sutProvider.Sut.UploadFileToExistingSendAsync(new MemoryStream(Encoding.UTF8.GetBytes(fileContents)), send);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateFileToExistingSendAsync_InvalidSize(SutProvider<SendService> sutProvider,
        Send send)
    {
        var fileContents = "Test file content";

        var sendFileData = new SendFileData
        {
            Id = "TEST",
            Size = fileContents.Length,
        };

        send.Type = SendType.File;
        send.Data = JsonSerializer.Serialize(sendFileData);

        sutProvider.GetDependency<ISendFileStorageService>()
            .ValidateFileAsync(send, sendFileData.Id, sendFileData.Size, Arg.Any<long>())
            .Returns((false, sendFileData.Size));

        var badRequest = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UploadFileToExistingSendAsync(new MemoryStream(Encoding.UTF8.GetBytes(fileContents)), send)
        );
    }

    [Theory]
    [BitAutoData]
    public void SendCanBeAccessed_Success(SutProvider<SendService> sutProvider, Send send)
    {
        var now = DateTime.UtcNow;
        send.MaxAccessCount = 10;
        send.AccessCount = 5;
        send.ExpirationDate = now.AddYears(1);
        send.DeletionDate = now.AddYears(1);
        send.Disabled = false;

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Any<User>(), send.Password, "TEST")
            .Returns(PasswordVerificationResult.Success);

        var (grant, passwordRequiredError, passwordInvalidError)
            = sutProvider.Sut.SendCanBeAccessed(send, "TEST");

        Assert.True(grant);
        Assert.False(passwordRequiredError);
        Assert.False(passwordInvalidError);
    }

    [Theory]
    [BitAutoData]
    public void SendCanBeAccessed_NullMaxAccess_Success(SutProvider<SendService> sutProvider,
        Send send)
    {
        var now = DateTime.UtcNow;
        send.MaxAccessCount = null;
        send.AccessCount = 5;
        send.ExpirationDate = now.AddYears(1);
        send.DeletionDate = now.AddYears(1);
        send.Disabled = false;

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Any<User>(), send.Password, "TEST")
            .Returns(PasswordVerificationResult.Success);

        var (grant, passwordRequiredError, passwordInvalidError)
            = sutProvider.Sut.SendCanBeAccessed(send, "TEST");

        Assert.True(grant);
        Assert.False(passwordRequiredError);
        Assert.False(passwordInvalidError);
    }

    [Theory]
    [BitAutoData]
    public void SendCanBeAccessed_NullSend_DoesNotGrantAccess(SutProvider<SendService> sutProvider)
    {
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Any<User>(), "TEST", "TEST")
            .Returns(PasswordVerificationResult.Success);

        var (grant, passwordRequiredError, passwordInvalidError)
            = sutProvider.Sut.SendCanBeAccessed(null, "TEST");

        Assert.False(grant);
        Assert.False(passwordRequiredError);
        Assert.False(passwordInvalidError);
    }

    [Theory]
    [BitAutoData]
    public void SendCanBeAccessed_NullPassword_PasswordRequiredErrorReturnsTrue(SutProvider<SendService> sutProvider,
        Send send)
    {
        var now = DateTime.UtcNow;
        send.MaxAccessCount = null;
        send.AccessCount = 5;
        send.ExpirationDate = now.AddYears(1);
        send.DeletionDate = now.AddYears(1);
        send.Disabled = false;
        send.Password = "HASH";

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Any<User>(), "TEST", "TEST")
            .Returns(PasswordVerificationResult.Success);

        var (grant, passwordRequiredError, passwordInvalidError)
            = sutProvider.Sut.SendCanBeAccessed(send, null);

        Assert.False(grant);
        Assert.True(passwordRequiredError);
        Assert.False(passwordInvalidError);
    }

    [Theory]
    [BitAutoData]
    public void SendCanBeAccessed_RehashNeeded_RehashesPassword(SutProvider<SendService> sutProvider,
        Send send)
    {
        var now = DateTime.UtcNow;
        send.MaxAccessCount = null;
        send.AccessCount = 5;
        send.ExpirationDate = now.AddYears(1);
        send.DeletionDate = now.AddYears(1);
        send.Disabled = false;
        send.Password = "TEST";

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Any<User>(), "TEST", "TEST")
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);

        var (grant, passwordRequiredError, passwordInvalidError)
            = sutProvider.Sut.SendCanBeAccessed(send, "TEST");

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .Received(1)
            .HashPassword(Arg.Any<User>(), "TEST");

        Assert.True(grant);
        Assert.False(passwordRequiredError);
        Assert.False(passwordInvalidError);
    }

    [Theory]
    [BitAutoData]
    public void SendCanBeAccessed_VerifyFailed_PasswordInvalidReturnsTrue(SutProvider<SendService> sutProvider,
        Send send)
    {
        var now = DateTime.UtcNow;
        send.MaxAccessCount = null;
        send.AccessCount = 5;
        send.ExpirationDate = now.AddYears(1);
        send.DeletionDate = now.AddYears(1);
        send.Disabled = false;
        send.Password = "TEST";

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Any<User>(), "TEST", "TEST")
            .Returns(PasswordVerificationResult.Failed);

        var (grant, passwordRequiredError, passwordInvalidError)
            = sutProvider.Sut.SendCanBeAccessed(send, "TEST");

        Assert.False(grant);
        Assert.False(passwordRequiredError);
        Assert.True(passwordInvalidError);
    }
}
