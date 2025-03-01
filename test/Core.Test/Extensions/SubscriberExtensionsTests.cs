using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Extensions;
using Xunit;

namespace Bit.Core.Test.Extensions;

public class SubscriberExtensionsTests
{
    [Theory]
    [InlineData("Alexandria Villanueva Gonzalez Pablo", "Alexandria Villanueva Gonzalez")]
    [InlineData("John Snow", "John Snow")]
    public void GetFormattedInvoiceName_Returns_FirstThirtyCaractersOfName(string name, string expected)
    {
        // arrange
        var provider = new Provider { Name = name };

        // act
        var actual = provider.GetFormattedInvoiceName();

        // assert
        Assert.Equal(expected, actual);
    }
}
