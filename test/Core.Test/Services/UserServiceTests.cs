using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Fido2NetLib;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
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

            sutProvider.GetDependency<Settings.GlobalSettings>().SelfHosted = true;
            sutProvider.GetDependency<Settings.GlobalSettings>().LicenseDirectory = tempDir.Directory;
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
    }
}
