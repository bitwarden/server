using Bit.Core.Models.Table;

namespace Bit.Core.Identity
{
    public class JwtBearerSignInResult
    {
        private static readonly JwtBearerSignInResult _success = new JwtBearerSignInResult { Succeeded = true };
        private static readonly JwtBearerSignInResult _failed = new JwtBearerSignInResult();
        private static readonly JwtBearerSignInResult _lockedOut = new JwtBearerSignInResult { IsLockedOut = true };
        private static readonly JwtBearerSignInResult _notAllowed = new JwtBearerSignInResult { IsNotAllowed = true };
        private static readonly JwtBearerSignInResult _twoFactorRequired = new JwtBearerSignInResult { RequiresTwoFactor = true };

        public bool Succeeded { get; protected set; }
        public bool IsLockedOut { get; protected set; }
        public bool IsNotAllowed { get; protected set; }
        public bool RequiresTwoFactor { get; protected set; }
        public string Token { get; set; }
        public User User { get; set; }

        public static JwtBearerSignInResult Success => _success;
        public static JwtBearerSignInResult Failed => _failed;
        public static JwtBearerSignInResult LockedOut => _lockedOut;
        public static JwtBearerSignInResult NotAllowed => _notAllowed;
        public static JwtBearerSignInResult TwoFactorRequired => _twoFactorRequired;

        public override string ToString()
        {
            return IsLockedOut ? "Lockedout" :
                   IsNotAllowed ? "NotAllowed" :
                   RequiresTwoFactor ? "RequiresTwoFactor" :
                   Succeeded ? "Succeeded" : "Failed";
        }
    }
}
