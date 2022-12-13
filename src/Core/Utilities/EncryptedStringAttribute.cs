using System.Buffers;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

#nullable enable

namespace Bit.Core.Utilities;

/// <summary>
/// Validates a string that is in encrypted form: "head.b64iv=|b64ct=|b64mac="
/// </summary>
public class EncryptedStringAttribute : ValidationAttribute
{
    internal static readonly Dictionary<EncryptionType, int> _encryptionTypeToRequiredPiecesMap = new()
    {
        [EncryptionType.AesCbc256_B64] = 2, // iv|ct
        [EncryptionType.AesCbc128_HmacSha256_B64] = 3, // iv|ct|mac
        [EncryptionType.AesCbc256_HmacSha256_B64] = 3, // iv|ct|mac
        [EncryptionType.Rsa2048_OaepSha256_B64] = 1, // rsaCt
        [EncryptionType.Rsa2048_OaepSha1_B64] = 1, // rsaCt
        [EncryptionType.Rsa2048_OaepSha256_HmacSha256_B64] = 2, // rsaCt|mac
        [EncryptionType.Rsa2048_OaepSha1_HmacSha256_B64] = 2, // rsaCt|mac
    };

    public EncryptedStringAttribute()
        : base("{0} is not a valid encrypted string.")
    { }

    public override bool IsValid(object? value)
    {
        try
        {
            if (value is null)
            {
                return true;
            }

            if (value is string stringValue)
            {
                // Fast path
                return IsValidCore(stringValue);
            }

            // This attribute should only be placed on string properties, fail
            return false;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsValidCore(ReadOnlySpan<char> value)
    {
        if (!value.TrySplitBy('.', out var headerChunk, out var rest))
        {
            // We couldn't find a header part, this is the slow path, because we have to do two loops over
            // the data.
            // If it has 3 encryption parts that means it is AesCbc128_HmacSha256_B64
            // else we assume it is AesCbc256_B64
            var splitChars = rest.Count('|');

            if (splitChars == 2)
            {
                return ValidatePieces(rest, _encryptionTypeToRequiredPiecesMap[EncryptionType.AesCbc128_HmacSha256_B64]);
            }
            else
            {
                return ValidatePieces(rest, _encryptionTypeToRequiredPiecesMap[EncryptionType.AesCbc256_B64]);
            }
        }

        EncryptionType encryptionType;

        // Using byte here because that is the backing type for EncryptionType
        if (!byte.TryParse(headerChunk, out var encryptionTypeNumber))
        {
            // We can't read the header chunk as a number, this is the slow path
            if (!Enum.TryParse(headerChunk, out encryptionType))
            {
                // Can't even get the enum from a non-number header, fail
                return false;
            }

            // Since this value came from Enum.TryParse we know it is an enumerated object and we can therefore
            // just access the dictionary
            return ValidatePieces(rest, _encryptionTypeToRequiredPiecesMap[encryptionType]);
        }

        // Simply cast the number to the enum, this could be a value that doesn't actually have a backing enum
        // entry but that is alright we will use it to look in the dictionary and non-valid 
        // numbers will be filtered out there.
        encryptionType = (EncryptionType)encryptionTypeNumber;

        if (!_encryptionTypeToRequiredPiecesMap.TryGetValue(encryptionType, out var encryptionPieces))
        {
            // Could not find a configuration map for the given header piece. This is an invalid string
            return false;
        }

        return ValidatePieces(rest, encryptionPieces);
    }

    private static bool ValidatePieces(ReadOnlySpan<char> encryptionPart, int requiredPieces)
    {
        var rest = encryptionPart;

        while (requiredPieces != 0)
        {
            if (requiredPieces == 1)
            {
                // Only one more part is needed so don't split and check the chunk
                if (!IsValidBase64(rest))
                {
                    return false;
                }

                // Make sure there isn't another split character possibly denoting another chunk
                return rest.IndexOf('|') == -1;
            }
            else
            {
                // More than one part is required so split it out
                if (!rest.TrySplitBy('|', out var chunk, out rest))
                {
                    return false;
                }

                // Is the required chunk valid base 64?
                if (!IsValidBase64(chunk))
                {
                    return false;
                }
            }

            // This current piece is valid so we can count down
            requiredPieces--;
        }

        // No more parts are required, so check there are no extra parts
        return rest.IndexOf('|') == -1;
    }

    private static bool IsValidBase64(ReadOnlySpan<char> input)
    {
        const int StackLimit = 256;

        byte[]? pooledChunks = null;

        var upperLimitLength = CalculateBase64ByteLengthUpperLimit(input.Length);

        // Ref: https://vcsjones.dev/stackalloc/
        var byteBuffer = upperLimitLength > StackLimit
            ? (pooledChunks = ArrayPool<byte>.Shared.Rent(upperLimitLength))
            : stackalloc byte[StackLimit];

        try
        {
            var successful = Convert.TryFromBase64Chars(input, byteBuffer, out var bytesWritten);
            return successful && bytesWritten > 0;
        }
        finally
        {
            // Check if we rented the pool and if so, return it.
            if (pooledChunks != null)
            {
                ArrayPool<byte>.Shared.Return(pooledChunks, true);
            }
        }
    }

    internal static int CalculateBase64ByteLengthUpperLimit(int charLength)
    {
        return 3 * (charLength / 4);
    }
}
