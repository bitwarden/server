namespace Bit.Core.Exceptions;

public class TxtRecordNotFoundException : Exception
{
    public TxtRecordNotFoundException()
        : base("TXT record not found.") { }
}
