using Bit.Core.Auth.Models;
using Bit.Core.Auth.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Utilities;

public class DuoUtilitiesTests
{
    [Theory]
    [BitAutoData]
    public void HasProperMetaData_ReturnsTrue(TwoFactorProvider twoFactorProvider)
    {
        twoFactorProvider.MetaData = DuoMetaData();
        var result = DuoUtilities.HasProperDuoMetadata(twoFactorProvider);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public void HasProperMetaData_ReturnsFalse(TwoFactorProvider twoFactorProvider)
    {
        twoFactorProvider.MetaData = null;
        var result = DuoUtilities.HasProperDuoMetadata(twoFactorProvider);

        Assert.False(result);
    }

    private Dictionary<string, object> DuoMetaData()
    {
        return new()
        {
            { "ClientId", "clientId" },
            { "ClientSecret", "clientSecret" },
            { "Host", "api-abcd1234.duosecurity.com" }
        };
    }
}
