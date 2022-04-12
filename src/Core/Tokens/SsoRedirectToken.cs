using System;
using System.Linq;

namespace Bit.Core.Tokens
{
    public static class SsoRedirectToken
    {
        #region Constants

        public const string SSO_REDIRECT_COOKIE_NAME    = "SsoRedirect";
        private const int MAX_REDIRECT_COOKIE_MINUTES   = 5;

        #endregion

        #region Public Methods

        public static string GenerateSsoRedirectToken()
        {
            byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            byte[] key = Guid.NewGuid().ToByteArray();
            string token = Convert.ToBase64String(time.Concat(key).ToArray());

            return token;
        }

        public static bool ValidateSsoRedirectToken(string token)
        {
            byte[] data = Convert.FromBase64String(token);
            DateTime when = DateTime.FromBinary(BitConverter.ToInt64(data, 0));
            Guid guid = default(Guid);
            var unparsedGuid = BitConverter.ToString(data.Skip(8).ToArray()).Replace("-", "");

            if (DateTime.UtcNow.AddMinutes(-MAX_REDIRECT_COOKIE_MINUTES) > when || !Guid.TryParse(unparsedGuid, out guid))
            {
                return false;
            }
            else
            {
                return true;
            }

        }

        #endregion
    }
}
