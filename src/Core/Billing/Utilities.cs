using Bit.Core.Exceptions;

namespace Bit.Core.Billing;

public static class Utilities
{
    public static GatewayException ContactSupport() => new("Something went wrong with your request. Please contact support.");
}
