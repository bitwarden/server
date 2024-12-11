namespace Bit.Core.Exceptions;

public class InvalidGatewayCustomerIdException : Exception
{
    public InvalidGatewayCustomerIdException()
        : base("Invalid gateway customerId.") { }
}
