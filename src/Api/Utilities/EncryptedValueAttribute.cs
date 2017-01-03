using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Utilities
{
    /// <summary>
    /// Validates a string that is in encrypted form: "b64iv=|b64ct="
    /// </summary>
    public class EncryptedStringAttribute : ValidationAttribute
    {
        public EncryptedStringAttribute()
            : base("{0} is not a valid encrypted string.")
        { }

        public override bool IsValid(object value)
        {
            if(value == null)
            {
                return true;
            }

            try
            {
                var encString = value?.ToString();
                if(string.IsNullOrWhiteSpace(encString))
                {
                    return false;
                }

                var encStringPieces = encString.Split('|');
                if(encStringPieces.Length != 2 && encStringPieces.Length != 3)
                {
                    return false;
                }

                var iv = Convert.FromBase64String(encStringPieces[0]);
                var ct = Convert.FromBase64String(encStringPieces[1]);

                if(iv.Length < 1 || ct.Length < 1)
                {
                    return false;
                }

                if(encStringPieces.Length == 3)
                {
                    var mac = Convert.FromBase64String(encStringPieces[2]);
                    if(mac.Length < 1)
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
