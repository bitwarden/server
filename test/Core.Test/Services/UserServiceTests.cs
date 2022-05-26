using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class UserServiceTests
    {
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpdateLicenseAsync_Success(SutProvider<UserService> sutProvider,
            User user, UserLicense userLicense)
        {
            using var tempDir = new TempDirectory();

            var now = DateTime.UtcNow;
            userLicense.Issued = now.AddDays(-10);
            userLicense.Expires = now.AddDays(10);
            userLicense.Version = 1;
            userLicense.Premium = true;

            user.EmailVerified = true;
            user.Email = userLicense.Email;

            sutProvider.GetDependency<Settings.IGlobalSettings>().SelfHosted = true;
            sutProvider.GetDependency<Settings.IGlobalSettings>().LicenseDirectory = tempDir.Directory;
            sutProvider.GetDependency<ILicensingService>()
                .VerifyLicense(userLicense)
                .Returns(true);

            await sutProvider.Sut.UpdateLicenseAsync(user, userLicense);

            var filePath = Path.Combine(tempDir.Directory, "user", $"{user.Id}.json");
            Assert.True(File.Exists(filePath));
            var document = JsonDocument.Parse(File.OpenRead(filePath));
            var root = document.RootElement;
            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            // Sort of a lazy way to test that it is indented but not sure of a better way
            Assert.Contains('\n', root.GetRawText());
            AssertHelper.AssertJsonProperty(root, "LicenseKey", JsonValueKind.String);
            AssertHelper.AssertJsonProperty(root, "Id", JsonValueKind.String);
            AssertHelper.AssertJsonProperty(root, "Premium", JsonValueKind.True);
            var versionProp = AssertHelper.AssertJsonProperty(root, "Version", JsonValueKind.Number);
            Assert.Equal(1, versionProp.GetInt32());
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SendTwoFactorEmailAsync_Success(SutProvider<UserService> sutProvider, User user)
        {
            var email = user.Email.ToLowerInvariant();
            var token = "thisisatokentocompare";

            var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
            userTwoFactorTokenProvider
                .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
                .Returns(Task.FromResult(true));
            userTwoFactorTokenProvider
                .GenerateAsync("2faEmail:" + email, Arg.Any<UserManager<User>>(), user)
                .Returns(Task.FromResult(token));

            sutProvider.Sut.RegisterTokenProvider("Email", userTwoFactorTokenProvider);

            user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
            {
                [TwoFactorProviderType.Email] = new TwoFactorProvider
                {
                    MetaData = new Dictionary<string, object> { ["Email"] = email },
                    Enabled = true
                }
            });
            await sutProvider.Sut.SendTwoFactorEmailAsync(user);

            await sutProvider.GetDependency<IMailService>()
                .Received(1)
                .SendTwoFactorEmailAsync(email, token);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SendTwoFactorEmailBecauseNewDeviceLoginAsync_Success(SutProvider<UserService> sutProvider, User user)
        {
            var email = user.Email.ToLowerInvariant();
            var token = "thisisatokentocompare";

            var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
            userTwoFactorTokenProvider
                .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
                .Returns(Task.FromResult(true));
            userTwoFactorTokenProvider
                .GenerateAsync("2faEmail:" + email, Arg.Any<UserManager<User>>(), user)
                .Returns(Task.FromResult(token));

            sutProvider.Sut.RegisterTokenProvider("Email", userTwoFactorTokenProvider);

            user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
            {
                [TwoFactorProviderType.Email] = new TwoFactorProvider
                {
                    MetaData = new Dictionary<string, object> { ["Email"] = email },
                    Enabled = true
                }
            });
            await sutProvider.Sut.SendTwoFactorEmailAsync(user, true);

            await sutProvider.GetDependency<IMailService>()
                .Received(1)
                .SendNewDeviceLoginTwoFactorEmailAsync(email, token);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderOnUser(SutProvider<UserService> sutProvider, User user)
        {
            user.TwoFactorProviders = null;

            await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderMetadataOnUser(SutProvider<UserService> sutProvider, User user)
        {
            user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
            {
                [TwoFactorProviderType.Email] = new TwoFactorProvider
                {
                    MetaData = null,
                    Enabled = true
                }
            });

            await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderEmailMetadataOnUser(SutProvider<UserService> sutProvider, User user)
        {
            user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
            {
                [TwoFactorProviderType.Email] = new TwoFactorProvider
                {
                    MetaData = new Dictionary<string, object> { ["qweqwe"] = user.Email.ToLowerInvariant() },
                    Enabled = true
                }
            });

            await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task Needs2FABecauseNewDeviceAsync_ReturnsTrue(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;
            user.UnknownDeviceVerificationEnabled = true;
            const string deviceIdToCheck = "7b01b586-b210-499f-8d52-0c3fdaa646fc";
            const string deviceIdInRepo = "ea29126c-91b7-4cc4-8ce6-00105b37f64a";

            sutProvider.GetDependency<IDeviceRepository>()
                       .GetManyByUserIdAsync(user.Id)
                       .Returns(Task.FromResult<ICollection<Device>>(new List<Device>
                       {
                            new Device { Identifier = deviceIdInRepo }
                       }));


            sutProvider.GetDependency<Settings.IGlobalSettings>().TwoFactorAuth.EmailOnNewDeviceLogin.Returns(true);

            Assert.True(await sutProvider.Sut.Needs2FABecauseNewDeviceAsync(user, deviceIdToCheck, "password"));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task Needs2FABecauseNewDeviceAsync_ReturnsFalse_When_GranType_Is_AuthorizationCode(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;
            const string deviceIdToCheck = "7b01b586-b210-499f-8d52-0c3fdaa646fc";
            const string deviceIdInRepo = "ea29126c-91b7-4cc4-8ce6-00105b37f64a";

            sutProvider.GetDependency<IDeviceRepository>()
                       .GetManyByUserIdAsync(user.Id)
                       .Returns(Task.FromResult<ICollection<Device>>(new List<Device>
                       {
                            new Device { Identifier = deviceIdInRepo }
                       }));

            Assert.False(await sutProvider.Sut.Needs2FABecauseNewDeviceAsync(user, deviceIdToCheck, "authorization_code"));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task Needs2FABecauseNewDeviceAsync_ReturnsFalse_When_Email_Is_Not_Verified(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = false;
            user.TwoFactorProviders = null;
            const string deviceIdToCheck = "7b01b586-b210-499f-8d52-0c3fdaa646fc";
            const string deviceIdInRepo = "ea29126c-91b7-4cc4-8ce6-00105b37f64a";

            sutProvider.GetDependency<IDeviceRepository>()
                       .GetManyByUserIdAsync(user.Id)
                       .Returns(Task.FromResult<ICollection<Device>>(new List<Device>
                       {
                            new Device { Identifier = deviceIdInRepo }
                       }));

            Assert.False(await sutProvider.Sut.Needs2FABecauseNewDeviceAsync(user, deviceIdToCheck, "password"));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task Needs2FABecauseNewDeviceAsync_ReturnsFalse_When_Is_The_First_Device(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;
            const string deviceIdToCheck = "7b01b586-b210-499f-8d52-0c3fdaa646fc";

            sutProvider.GetDependency<IDeviceRepository>()
                       .GetManyByUserIdAsync(user.Id)
                       .Returns(Task.FromResult<ICollection<Device>>(new List<Device>()));

            Assert.False(await sutProvider.Sut.Needs2FABecauseNewDeviceAsync(user, deviceIdToCheck, "password"));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task Needs2FABecauseNewDeviceAsync_ReturnsFalse_When_DeviceId_Is_Already_In_Repo(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;
            const string deviceIdToCheck = "7b01b586-b210-499f-8d52-0c3fdaa646fc";

            sutProvider.GetDependency<IDeviceRepository>()
                       .GetManyByUserIdAsync(user.Id)
                       .Returns(Task.FromResult<ICollection<Device>>(new List<Device>
                       {
                            new Device { Identifier = deviceIdToCheck }
                       }));

            Assert.False(await sutProvider.Sut.Needs2FABecauseNewDeviceAsync(user, deviceIdToCheck, "password"));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task Needs2FABecauseNewDeviceAsync_ReturnsFalse_When_GlobalSettings_2FA_EmailOnNewDeviceLogin_Is_Disabled(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;
            const string deviceIdToCheck = "7b01b586-b210-499f-8d52-0c3fdaa646fc";
            const string deviceIdInRepo = "ea29126c-91b7-4cc4-8ce6-00105b37f64a";

            sutProvider.GetDependency<IDeviceRepository>()
                       .GetManyByUserIdAsync(user.Id)
                       .Returns(Task.FromResult<ICollection<Device>>(new List<Device>
                       {
                            new Device { Identifier = deviceIdInRepo }
                       }));

            sutProvider.GetDependency<Settings.IGlobalSettings>().TwoFactorAuth.EmailOnNewDeviceLogin.Returns(false);

            Assert.False(await sutProvider.Sut.Needs2FABecauseNewDeviceAsync(user, deviceIdToCheck, "password"));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task Needs2FABecauseNewDeviceAsync_ReturnsFalse_When_UnknownDeviceVerification_Is_Disabled(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;
            user.UnknownDeviceVerificationEnabled = false;
            const string deviceIdToCheck = "7b01b586-b210-499f-8d52-0c3fdaa646fc";
            const string deviceIdInRepo = "ea29126c-91b7-4cc4-8ce6-00105b37f64a";

            sutProvider.GetDependency<IDeviceRepository>()
                       .GetManyByUserIdAsync(user.Id)
                       .Returns(Task.FromResult<ICollection<Device>>(new List<Device>
                       {
                            new Device { Identifier = deviceIdInRepo }
                       }));

            sutProvider.GetDependency<Settings.IGlobalSettings>().TwoFactorAuth.EmailOnNewDeviceLogin.Returns(true);

            Assert.False(await sutProvider.Sut.Needs2FABecauseNewDeviceAsync(user, deviceIdToCheck, "password"));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public void CanEditDeviceVerificationSettings_ReturnsTrue(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;

            sutProvider.GetDependency<Settings.IGlobalSettings>().TwoFactorAuth.EmailOnNewDeviceLogin.Returns(true);

            Assert.True(sutProvider.Sut.CanEditDeviceVerificationSettings(user));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public void CanEditDeviceVerificationSettings_ReturnsFalse_When_GlobalSettings_2FA_EmailOnNewDeviceLogin_Is_Disabled(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;

            sutProvider.GetDependency<Settings.IGlobalSettings>().TwoFactorAuth.EmailOnNewDeviceLogin.Returns(false);

            Assert.False(sutProvider.Sut.CanEditDeviceVerificationSettings(user));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public void CanEditDeviceVerificationSettings_ReturnsFalse_When_Email_Is_Not_Verified(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = false;
            user.TwoFactorProviders = null;

            sutProvider.GetDependency<Settings.IGlobalSettings>().TwoFactorAuth.EmailOnNewDeviceLogin.Returns(true);

            Assert.False(sutProvider.Sut.CanEditDeviceVerificationSettings(user));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public void CanEditDeviceVerificationSettings_ReturnsFalse_When_User_Uses_Key_Connector(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.TwoFactorProviders = null;
            user.UsesKeyConnector = true;

            sutProvider.GetDependency<Settings.IGlobalSettings>().TwoFactorAuth.EmailOnNewDeviceLogin.Returns(true);

            Assert.False(sutProvider.Sut.CanEditDeviceVerificationSettings(user));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public void CanEditDeviceVerificationSettings_ReturnsFalse_When_User_Has_A_2FA_Already_Set_Up(SutProvider<UserService> sutProvider, User user)
        {
            user.EmailVerified = true;
            user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
            {
                [TwoFactorProviderType.Email] = new TwoFactorProvider
                {
                    MetaData = new Dictionary<string, object> { ["Email"] = "asdfasf" },
                    Enabled = true
                }
            });

            sutProvider.GetDependency<Settings.IGlobalSettings>().TwoFactorAuth.EmailOnNewDeviceLogin.Returns(true);

            Assert.False(sutProvider.Sut.CanEditDeviceVerificationSettings(user));
        }
    }
}
