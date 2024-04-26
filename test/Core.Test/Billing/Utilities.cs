using Bit.Core.Exceptions;
using Xunit;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Test.Billing;

public static class Utilities
{
    public static async Task ThrowsContactSupportAsync(
        Func<Task> function,
        string internalMessage = null,
        Exception innerException = null)
    {
        var contactSupport = ContactSupport(internalMessage, innerException);

        var exception = await Assert.ThrowsAsync<GatewayException>(function);

        Assert.Equal(contactSupport.ClientFriendlyMessage, exception.ClientFriendlyMessage);
        Assert.Equal(contactSupport.Message, exception.Message);
        Assert.Equal(contactSupport.InnerException, exception.InnerException);
    }
}
