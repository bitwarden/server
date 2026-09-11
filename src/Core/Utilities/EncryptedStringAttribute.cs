using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

#nullable enable

namespace Bit.Core.Utilities;

/// <summary>
/// Validates a string that is in encrypted form: "head.b64iv=|b64ct=|b64mac="
/// </summary>
public class EncryptedStringAttribute : ValidationAttribute
{
    private const int _ivBytes = 16;
    private const int _macBytes = 32; // HMAC-SHA256
    private const int _anyLength = -1; // The piece has no fixed decoded length

    /// <summary>
    /// The expected decoded byte length of every piece of an encrypted string, per encryption type.
    /// The number of entries is the number of required pieces.
    /// </summary>
    internal static readonly Dictionary<EncryptionType, int[]> _encryptionTypeToRequiredPiecesMap = new()
    {
        [EncryptionType.AesCbc256_B64] = [_ivBytes, _anyLength], // iv|ct
        [EncryptionType.AesCbc256_HmacSha256_B64] = [_ivBytes, _anyLength, _macBytes], // iv|ct|mac
        [EncryptionType.CoseEncrypt0B64] = [_anyLength], // cose bytes
        [EncryptionType.Rsa2048_OaepSha256_B64] = [_anyLength], // rsaCt
        [EncryptionType.Rsa2048_OaepSha1_B64] = [_anyLength], // rsaCt
        [EncryptionType.Rsa2048_OaepSha256_HmacSha256_B64] = [_anyLength, _anyLength], // rsaCt|mac
        [EncryptionType.Rsa2048_OaepSha1_HmacSha256_B64] = [_anyLength, _anyLength], // rsaCt|mac
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
            // If it has 3 encryption parts it is the legacy headerless iv|ct|mac format, which shares
            // its piece layout with AesCbc256_HmacSha256_B64, else we assume it is AesCbc256_B64
            var splitChars = rest.Count('|');

            if (splitChars == 2)
            {
                return ValidatePieces(rest, _encryptionTypeToRequiredPiecesMap[EncryptionType.AesCbc256_HmacSha256_B64]);
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

        if (!_encryptionTypeToRequiredPiecesMap.TryGetValue(encryptionType, out var expectedByteLengths))
        {
            // Could not find a configuration map for the given header piece. This is an invalid string
            return false;
        }

        return ValidatePieces(rest, expectedByteLengths);
    }

    private static bool ValidatePieces(ReadOnlySpan<char> encryptionPart, ReadOnlySpan<int> expectedByteLengths)
    {
        var rest = encryptionPart;

        for (var i = 0; i < expectedByteLengths.Length; i++)
        {
            var expectedByteLength = expectedByteLengths[i];

            if (i == expectedByteLengths.Length - 1)
            {
                // Only one more part is needed so don't split and check the chunk
                if (!IsValidPiece(rest, expectedByteLength))
                {
                    return false;
                }

                // Make sure there isn't another split character possibly denoting another chunk
                return rest.IndexOf('|') == -1;
            }

            // More than one part is required so split it out
            if (!rest.TrySplitBy('|', out var chunk, out rest))
            {
                return false;
            }

            if (!IsValidPiece(chunk, expectedByteLength))
            {
                return false;
            }
        }

        // No more parts are required, so check there are no extra parts
        return rest.IndexOf('|') == -1;
    }

    private static bool IsValidPiece(ReadOnlySpan<char> piece, int expectedByteLength)
    {
        if (piece.IsEmpty || !IsValidBase64Permissive(piece))
        {
            return false;
        }

        return expectedByteLength == _anyLength || DecodedByteLength(piece) == expectedByteLength;
    }

    /// <summary>
    /// The number of bytes a valid base64 piece decodes to. E.g. "AAECAwQFBgcICQoLDA0ODw==" -> 24 / 4 * 3 - 2 = 16.
    /// </summary>
    private static int DecodedByteLength(ReadOnlySpan<char> piece)
    {
        var padCount = 0;
        if (piece[^1] == '=') { padCount++; if (piece[^2] == '=') { padCount++; } }

        return piece.Length / 4 * 3 - padCount;
    }

    private const string _base64Chars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    private static int Base64CharValue(char c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a' + 26,
        >= '0' and <= '9' => c - '0' + 52,
        '+' => 62,
        '/' => 63,
        _ => -1,
    };

    /// <summary>
    /// Validates base64 permissively by accepting non-zero padding bits.
    /// </summary>
    private static bool IsValidBase64Permissive(ReadOnlySpan<char> value)
    {
        // Obviously not base64
        if (value.IsEmpty || value.Length % 4 != 0)
            return false;

        // If there isn't any padding, there's nothing to be permissive about.
        var padCount = 0;
        if (value[^1] == '=') { padCount++; if (value[^2] == '=') padCount++; }
        if (padCount == 0)
            return Base64.IsValid(value);

        // Get the last non-padding char. Ensure it's in the base64 alphabet.
        var lastDataIdx = value.Length - padCount - 1;
        var charVal = Base64CharValue(value[lastDataIdx]);
        if (charVal < 0)
            return false;

        // Compute the correct char. If the original char is already valid,
        // test the full string.
        var dataBitMask = padCount == 2 ? 0b110000 : 0b111100;
        var newCharVal = charVal & dataBitMask;
        if (newCharVal == charVal)
            return Base64.IsValid(value);

        // Validate all but the last block, to minimize allocation in the next
        // section.
        if (value.Length > 4 && !Base64.IsValid(value[..^4]))
            return false;

        // Apply the correct char and validate the last block 
        Span<char> canonical = stackalloc char[4];
        value[^4..].CopyTo(canonical);
        canonical[4 - padCount - 1] = _base64Chars[newCharVal];
        return Base64.IsValid(canonical);
    }
}
