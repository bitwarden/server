using System;

namespace Bit.Core.Tokenizer
{
    public class BadTokenException : Exception
    {
        public BadTokenException()
        {
        }

        public BadTokenException(string message) : base(message)
        {
        }
    }
}
