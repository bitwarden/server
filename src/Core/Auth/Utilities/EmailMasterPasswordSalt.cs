using System.Globalization;
using System.Text;

namespace Bit.Core.Auth.Utilities;

/// <summary>
/// Gives the master password salt that clients derive from an email. Clients trim the email and lowercase it with the
/// Unicode full lowercase mapping, and they derive the master key from that value.
/// </summary>
public static class EmailMasterPasswordSalt
{
    private const char _capitalIWithDotAbove = '\u0130';
    private const string _lowercaseIWithCombiningDotAbove = "i\u0307";
    private const char _capitalSigma = '\u03A3';
    private const char _smallSigma = '\u03C3';
    private const char _smallFinalSigma = '\u03C2';

    /// <summary>
    /// Trims the email and lowercases it with the Unicode full lowercase mapping.
    /// </summary>
    /// <remarks>
    /// <see cref="string.ToLowerInvariant"/> uses the simple lowercase mapping. The full mapping differs from it in
    /// two places:
    /// <list type="number">
    ///   <item>İ (U+0130) becomes i followed by U+0307. <see cref="string.ToLowerInvariant"/> does not change İ.</item>
    ///   <item>Σ (U+03A3) becomes the final form ς when it ends a word, as defined by the Unicode Final_Sigma
    ///   condition. <see cref="string.ToLowerInvariant"/> always gives σ.</item>
    /// </list>
    /// </remarks>
    public static string FromEmail(string email)
    {
        var trimmed = email.Trim();
        var mapped = new StringBuilder(trimmed.Length);
        for (var i = 0; i < trimmed.Length; i++)
        {
            switch (trimmed[i])
            {
                case _capitalIWithDotAbove:
                    mapped.Append(_lowercaseIWithCombiningDotAbove);
                    break;
                case _capitalSigma:
                    mapped.Append(IsFinalSigma(trimmed, i) ? _smallFinalSigma : _smallSigma);
                    break;
                default:
                    mapped.Append(trimmed[i]);
                    break;
            }
        }

        // The characters added above are already lowercase, so this changes only the other characters.
        return mapped.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// The Unicode Final_Sigma condition: a cased letter comes before the sigma, and no cased letter comes after it.
    /// Case-ignorable characters on either side are skipped.
    /// </summary>
    private static bool IsFinalSigma(string text, int sigmaIndex)
    {
        return IsCasedAfterCaseIgnorable(text, sigmaIndex - 1, backward: true) &&
               !IsCasedAfterCaseIgnorable(text, sigmaIndex + 1, backward: false);
    }

    private static bool IsCasedAfterCaseIgnorable(string text, int index, bool backward)
    {
        while (backward ? index >= 0 : index < text.Length)
        {
            var status = backward
                ? Rune.DecodeLastFromUtf16(text.AsSpan(0, index + 1), out var rune, out var length)
                : Rune.DecodeFromUtf16(text.AsSpan(index), out rune, out length);
            if (status != System.Buffers.OperationStatus.Done)
            {
                return false;
            }

            if (!IsCaseIgnorable(rune))
            {
                return IsCased(rune);
            }

            index += backward ? -length : length;
        }

        return false;
    }

    /// <summary>
    /// The Unicode Cased property: Lowercase, Uppercase, or titlecase letter. Other_Lowercase and Other_Uppercase
    /// characters that are not case-ignorable are listed here. The case-ignorable ones are skipped before this check.
    /// </summary>
    private static bool IsCased(Rune rune)
    {
        switch (Rune.GetUnicodeCategory(rune))
        {
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
                return true;
        }

        var value = rune.Value;
        return value is 0x00AA or 0x00BA
            or (>= 0x2160 and <= 0x217F)
            or (>= 0x24B6 and <= 0x24E9)
            or (>= 0x1F130 and <= 0x1F149)
            or (>= 0x1F150 and <= 0x1F169)
            or (>= 0x1F170 and <= 0x1F189);
    }

    /// <summary>
    /// The Unicode Case_Ignorable property: the Mn, Me, Cf, Lm, and Sk categories, plus the Word_Break values
    /// MidLetter, MidNumLet, and Single_Quote.
    /// </summary>
    private static bool IsCaseIgnorable(Rune rune)
    {
        switch (Rune.GetUnicodeCategory(rune))
        {
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.Format:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.ModifierSymbol:
                return true;
        }

        return rune.Value is 0x0027 or 0x002E or 0x003A or 0x00B7 or 0x0387 or 0x055F or 0x05F4 or 0x2018
            or 0x2019 or 0x2024 or 0x2027 or 0xFE13 or 0xFE52 or 0xFE55 or 0xFF07 or 0xFF0E or 0xFF1A;
    }
}
