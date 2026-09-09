namespace Bit.Core.Tools.Enums;

public enum SendEncryptionType : byte
{
    // Send data is stored as a stringified JSON object that uses per-field encryption
    V1 = 1,
}
