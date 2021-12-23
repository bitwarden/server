using Bit.Core.Tokenizer;

namespace Bit.Core.Test.Tokenizer
{
    public class TestToken : ITokenable
    {
        private bool? _valid;
        public bool Valid
        {
            get => _valid ?? true;
            set => _valid = value;
        }
    }

    public class TestExpiringToken : ExpiringToken
    {
        private bool _forceInvalid;

        public TestExpiringToken() : this(false) { }

        public TestExpiringToken(bool forceInvalid)
        {
            _forceInvalid = forceInvalid;
        }
        protected override bool TokenIsValid() => !_forceInvalid;
    }
}
