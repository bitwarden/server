using System;

namespace Bit.Core.Tokenizer
{
    public abstract class ExpiringToken : ITokenable
    {
        public DateTime ExpirationDate { get; set; }
        public bool IsValid() => ExpirationDate < DateTime.UtcNow && TokenIsValid();
        protected abstract bool TokenIsValid();
    }
}
