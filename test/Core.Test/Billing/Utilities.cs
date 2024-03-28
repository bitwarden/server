using Bit.Core.Billing;
using Xunit;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Test.Billing;

public static class Utilities
{
    public static async Task ThrowsContactSupportAsync(Func<Task> function)
    {
        var contactSupport = ContactSupport();

        var exception = await Assert.ThrowsAsync<BillingException>(function);

        Assert.Equal(contactSupport.Message, exception.Message);
    }
}
