using Bit.Core.Billing;
using Xunit;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Test.Billing;

public static class Utilities
{
    public static async Task ThrowsBillingExceptionAsync(
        Func<Task> function,
        string responseMessage = null,
        string message = null,
        Exception innerException = null)
    {
        var expected = new BillingException(responseMessage, message, innerException);

        var actual = await Assert.ThrowsAsync<BillingException>(function);

        Assert.Equal(expected.ResponseMessage, actual.ResponseMessage);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.InnerException, actual.InnerException);
    }
}
