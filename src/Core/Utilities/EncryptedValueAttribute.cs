using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Utilities
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

                var headerPieces = encString.Split('.');
                string[] encStringPieces = null;
                var encType = Enums.EncryptionType.AesCbc256_B64;

                if(headerPieces.Length == 1)
                {
                    encStringPieces = headerPieces[0].Split('|');
                    if(encStringPieces.Length == 3)
                    {
                        encType = Enums.EncryptionType.AesCbc128_HmacSha256_B64;
                    }
                    else
                    {
                        encType = Enums.EncryptionType.AesCbc256_B64;
                    }
                }
                else if(headerPieces.Length == 2)
                {
                    encStringPieces = headerPieces[1].Split('|');
                    if(!Enum.TryParse(headerPieces[0], out encType))
                    {
                        return false;
                    }
                }

                switch(encType)
                {
                    case Enums.EncryptionType.AesCbc256_B64:
                        if(encStringPieces.Length != 2)
                        {
                            return false;
                        }
                        break;
                    case Enums.EncryptionType.AesCbc128_HmacSha256_B64:
                    case Enums.EncryptionType.AesCbc256_HmacSha256_B64:
                        if(encStringPieces.Length != 3)
                        {
                            return false;
                        }
                        break;
                    case Enums.EncryptionType.RsaOaep_Sha256_B64:
                        if(encStringPieces.Length != 1)
                        {
                            return false;
                        }
                        break;
                    default:
                        return false;
                }

                if(encType != Enums.EncryptionType.RsaOaep_Sha256_B64)
                {
                    var iv = Convert.FromBase64String(encStringPieces[0]);
                    var ct = Convert.FromBase64String(encStringPieces[1]);

                    if(iv.Length < 1 || ct.Length < 1)
                    {
                        return false;
                    }
                }

                if(encType == Enums.EncryptionType.AesCbc128_HmacSha256_B64 ||
                    encType == Enums.EncryptionType.AesCbc256_HmacSha256_B64)
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
