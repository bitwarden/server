using Bit.Core.Tokens;

namespace Bit.Core.Test.Tokens
{
    public class TestTokenable : Tokenable
    {
        public override bool Valid => true;
    }

    public class TestExpiringTokenable : ExpiringTokenable
    {
        private bool _forceInvalid;

        public TestExpiringTokenable() : this(false) { }

        public TestExpiringTokenable(bool forceInvalid)
        {
            _forceInvalid = forceInvalid;
        }
        protected override bool TokenIsValid() => !_forceInvalid;
    }
}
