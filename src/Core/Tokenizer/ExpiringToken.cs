using System;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;

namespace Bit.Core.Tokenizer
{
    public abstract class ExpiringToken : ITokenable
    {
        [JsonConverter(typeof(EpochDateTimeJsonConverter))]
        public DateTime ExpirationDate { get; set; }
        public bool Valid => ExpirationDate > DateTime.UtcNow && TokenIsValid();
        protected abstract bool TokenIsValid();
    }
}
