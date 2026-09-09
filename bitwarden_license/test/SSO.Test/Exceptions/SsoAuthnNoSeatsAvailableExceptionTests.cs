using Bit.Sso.Exceptions;

namespace Bit.SSO.Test.Exceptions;

public class SsoAuthnNoSeatsAvailableExceptionTests
{
    [Fact]
    public void Constructor_SetsDescriptiveMessage()
    {
        var ex = new SsoAuthnNoSeatsAvailableException();

        // Message is a static string used for stack-trace / debugger diagnosability
        // (nothing consumes it as data). Just sanity-check that it conveys the
        // failure mode rather than being a bare exception with no context.
        Assert.Contains("no seats available", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
