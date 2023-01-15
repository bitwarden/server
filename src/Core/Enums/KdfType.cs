namespace Bit.Core.Enums;

public enum KdfType : byte
{
    PBKDF2_SHA256 = 0,
    Scrypt = 1,
    Argon2id = 2
}
