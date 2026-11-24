using Bit.Core.Auth.Utilities;
using Xunit;

namespace Bit.Core.Test.Auth.Utilities;

public class EmailMasterPasswordSaltTests
{
    /// <summary>
    /// The expected values are the output of the Unicode full lowercase mapping after a trim. The cases cover İ and
    /// the word-final Σ rule, where the full mapping differs from <see cref="string.ToLowerInvariant"/>.
    /// </summary>
    [Theory]
    [InlineData(" User@Example.com ", "user@example.com")] // Trimmed and lowercased.
    [InlineData("user@ÜBER.de", "user@über.de")]
    [InlineData("user@İzmir.com.tr", "user@i\u0307zmir.com.tr")] // İ becomes i + U+0307.
    [InlineData("user@I\u0307zmir.com.tr", "user@i\u0307zmir.com.tr")] // Decomposed İ.
    [InlineData("user@ΣΟΦΙΑ.gr", "user@σοφια.gr")] // No letter before Σ.
    [InlineData("user@Σ-net.gr", "user@σ-net.gr")] // No letter before Σ.
    [InlineData("user@ΟΔΟΣ.gr", "user@οδοσ.gr")] // '.' is skipped and a letter follows.
    [InlineData("user@aΣ'b.com", "user@aσ'b.com")] // An apostrophe is skipped and a letter follows.
    [InlineData("user@aΣ\u0301b.com", "user@aσ\u0301b.com")] // A combining mark is skipped and a letter follows.
    [InlineData("user@ΑΣΣ-b.com", "user@ασς-b.com")] // Only the last Σ ends the word.
    [InlineData("user@ΟΔΟΣ-net.gr", "user@οδος-net.gr")] // Σ before '-'.
    [InlineData("user@ΟΔΟΣ1.gr", "user@οδος1.gr")] // Σ before a digit.
    [InlineData("user@ΟΔΟΣ.1x.gr", "user@οδος.1x.gr")] // '.' is skipped and a digit follows.
    [InlineData("user@aΣ\u0301-b.com", "user@aς\u0301-b.com")] // A combining mark is skipped and '-' follows.
    [InlineData("user@example.ΑΣ", "user@example.ας")] // Σ at the end of the email.
    [InlineData("user@a.Σ-b.com", "user@a.ς-b.com")] // '.' before Σ is skipped and a letter comes before it.
    [InlineData("user@ʰΣ-b.com", "user@ʰσ-b.com")] // A modifier letter is skipped and no letter comes before it.
    [InlineData("user@aʰΣ-b.com", "user@aʰς-b.com")] // A modifier letter is skipped and a letter comes before it.
    [InlineData("user@ªΣ-b.com", "user@ªς-b.com")] // ª is cased.
    [InlineData("user@ⒶΣ-b.com", "user@ⓐς-b.com")] // Ⓐ is cased.
    [InlineData("user@\U00010400Σ-b.com", "user@\U00010428ς-b.com")] // A cased letter outside the Basic Multilingual Plane.
    public void FromEmail_TrimsAndAppliesTheFullLowercaseMapping(string email, string expected)
    {
        Assert.Equal(expected, EmailMasterPasswordSalt.FromEmail(email));
    }
}
