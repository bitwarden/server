using Bit.Core.Billing;
using Xunit;

namespace Bit.Core.Test.Billing;

public static class Utilities
{
    public static async Task ThrowsBillingExceptionAsync(
        Func<Task> function,
        string response = null,
        string message = null,
        Exception innerException = null
    )
    {
        var expected = new BillingException(response, message, innerException);

        var actual = await Assert.ThrowsAsync<BillingException>(function);

        Assert.Equal(expected.Response, actual.Response);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.InnerException, actual.InnerException);
    }
}
