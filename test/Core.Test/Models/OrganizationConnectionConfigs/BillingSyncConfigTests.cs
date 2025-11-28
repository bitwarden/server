using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Models.OrganizationConnectionConfigs;

public class BillingSyncConfigTests
{
    [Theory]
    [BitAutoData]
    public void BillingSyncConfig_CanUse_Success(string billingSyncKey)
    {
        var config = new BillingSyncConfig() { BillingSyncKey = billingSyncKey };

        Assert.True(config.Validate(out var exception));
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Fact]
    public void BillingSyncConfig_CanUse_WhenNoKey_ReturnsFalse()
    {
        var config = new BillingSyncConfig();

        Assert.False(config.Validate(out var exception));
        Assert.Contains("Failed to get Billing Sync Key", exception);
    }
}
