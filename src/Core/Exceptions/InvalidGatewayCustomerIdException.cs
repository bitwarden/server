namespace Bit.Core.Exceptions;

#nullable enable

public class InvalidGatewayCustomerIdException : Exception
{
    public InvalidGatewayCustomerIdException()
        : base("Invalid gateway customerId.")
    {

    }
}
