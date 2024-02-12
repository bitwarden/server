using Bit.Core.Exceptions;
using Xunit;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Test.Billing;

public static class Utilities
{
    public static async Task ThrowsContactSupportAsync(Func<Task> function)
    {
        var contactSupport = ContactSupport();

        var exception = await Assert.ThrowsAsync<GatewayException>(function);

        Assert.Equal(contactSupport.Message, exception.Message);
    }
}
