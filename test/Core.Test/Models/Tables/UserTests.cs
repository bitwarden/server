using System.Collections.Generic;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Models.Tables
{
    public class UserTests
    {
        //                              KB     MB     GB
        public const long Multiplier = 1024 * 1024 * 1024;

        [Fact]
        public void StorageBytesRemaining_HasMax_DoesNotHaveStorage_ReturnsMaxAsBytes()
        {
            short maxStorageGb = 1;

            var user = new User
            {
                MaxStorageGb = maxStorageGb,
                Storage = null,
            };

            var bytesRemaining = user.StorageBytesRemaining();

            Assert.Equal(bytesRemaining, maxStorageGb * Multiplier);
        }

        [Theory]
        [InlineData(2, 1 * Multiplier, 1 * Multiplier)]

        public void StorageBytesRemaining_HasMax_HasStorage_ReturnRemainingStorage(short maxStorageGb, long storageBytes, long expectedRemainingBytes)
        {
            var user = new User
            {
                MaxStorageGb = maxStorageGb,
                Storage = storageBytes,
            };

            var bytesRemaining = user.StorageBytesRemaining();

            Assert.Equal(expectedRemainingBytes, bytesRemaining);
        }

        [Fact]
        public void SetTwoFactorProviders()
        {
            var user = new User();
            user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
            {
               [TwoFactorProviderType.WebAuthn] = new TwoFactorProvider
               {
                   Enabled = true,
                   MetaData = new Dictionary<string, object>
                   {
                       ["Item"] = "thing",
                   },
               },
               [TwoFactorProviderType.Email] = new TwoFactorProvider
               {
                   Enabled = false,
                   MetaData = new Dictionary<string, object>
                   {
                       ["Email"] = "test@email.com",
                   },
               },
            });

            using var jsonDocument = JsonDocument.Parse(user.TwoFactorProviders);
            var root = jsonDocument.RootElement;
            
            var webAuthn = AssertHelper.AssertJsonProperty(root, "WebAuthn", JsonValueKind.Object);
            AssertHelper.AssertJsonProperty(webAuthn, "Enabled", JsonValueKind.True);
            var webMetaData = AssertHelper.AssertJsonProperty(webAuthn, "MetaData", JsonValueKind.Object);
            AssertHelper.AssertJsonProperty(webMetaData, "Item", JsonValueKind.String);

            var email = AssertHelper.AssertJsonProperty(root, "Email", JsonValueKind.Object);
            AssertHelper.AssertJsonProperty(email, "Enabled", JsonValueKind.False);
            var emailMetaData = AssertHelper.AssertJsonProperty(email, "MetaData", JsonValueKind.Object);
            AssertHelper.AssertJsonProperty(emailMetaData, "Email", JsonValueKind.String);
        }
    }
}
