using System;

namespace Bit.Core.Exceptions
{
    public class StripeEmailSyncException : Exception
    {
        public StripeEmailSyncException()
            : base("Stripe email address could not be updated") { }
    }
}
