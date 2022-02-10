using System.Collections.Generic;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Entities
{
    public class OrganizationTests
    {
        [Fact]
        public void SetTwoFactorProviders_Success()
        {
            var organization = new Organization();
            organization.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
            {
                [TwoFactorProviderType.OrganizationDuo] = new TwoFactorProvider
                {
                    Enabled = true,
                    MetaData = new Dictionary<string, object>
                    {
                        ["IKey"] = "IKey_value",
                        ["SKey"] = "SKey_value",
                        ["Host"] = "Host_value",
                    },
                }
            });

            using var jsonDocument = JsonDocument.Parse(organization.TwoFactorProviders);
            var root = jsonDocument.RootElement;

            var duo = AssertHelper.AssertJsonProperty(root, "6", JsonValueKind.Object);
            AssertHelper.AssertJsonProperty(duo, "Enabled", JsonValueKind.True);
            var duoMetaData = AssertHelper.AssertJsonProperty(duo, "MetaData", JsonValueKind.Object);
            var iKey = AssertHelper.AssertJsonProperty(duoMetaData, "IKey", JsonValueKind.String).GetString();
            Assert.Equal("IKey_value", iKey);
            var sKey = AssertHelper.AssertJsonProperty(duoMetaData, "SKey", JsonValueKind.String).GetString();
            Assert.Equal("SKey_value", sKey);
            var host = AssertHelper.AssertJsonProperty(duoMetaData, "Host", JsonValueKind.String).GetString();
            Assert.Equal("Host_value", host);
        }
    }
}
