using Bit.Core.Tokens;

namespace Bit.Core.Test.Tokenizer
{
    public class TestToken : Tokenable
    {
        private bool? _valid;
        public override bool Valid
        {
            get => _valid ?? true;
        }
    }

    public class TestExpiringToken : ExpiringTokenable
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
