using Bit.Services.Pam.Api.Models.Request;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

/// <summary>
/// Unit tests for <see cref="CidrValidator.IsValid"/>. The test data mirrors the positive and
/// negative tables from the Rust SDK's <c>is_valid_cidr</c> tests in
/// <c>bitwarden-pam/src/access_rules/validate.rs</c>.
/// </summary>
public class CidrValidatorTests
{
    // -------------------------------------------------------------------------
    // Valid CIDRs
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("10.0.0.0/8")]
    [InlineData("192.168.0.0/16")]
    [InlineData("0.0.0.0/0")]
    [InlineData("255.255.255.255/32")]
    [InlineData("10.0.0.1/32")]       // /32 single host — no host bits
    [InlineData("::/0")]
    [InlineData("::1/128")]
    [InlineData("2001:db8::/32")]
    [InlineData("fe80::/10")]
    [InlineData("10.0.0.0/08")]       // leading zero on prefix — valid (SDK: prefix_with_leading_zero_is_valid)
    [InlineData("2001:db8::1/128")]   // IPv6 single-host /128 — valid (SDK: ipv6_full_prefix_is_valid)
    // IPv6 non-canonical-but-value-equivalent forms — valid (Rust Ipv6Addr::from_str compares by value)
    [InlineData("2001:0db8::/32")]    // leading zero in hextet
    [InlineData("2001:DB8::/32")]     // uppercase hextets
    [InlineData("2001:db8:0:0:0:0:0:0/32")] // fully uncompressed form
    public void IsValid_ValidCidrs_ReturnsTrue(string cidr)
    {
        Assert.True(CidrValidator.IsValid(cidr));
    }

    // -------------------------------------------------------------------------
    // Invalid CIDRs
    // -------------------------------------------------------------------------

    [Theory]
    // Host bits set (IPv4)
    [InlineData("10.0.0.1/8", "host bits set")]
    [InlineData("10.0.0.0/0", "IPv4 /0 with non-zero address has host bits set")]
    // Prefix out of range
    [InlineData("10.0.0.0/33", "IPv4 prefix > 32")]
    [InlineData("2001:db8::/129", "IPv6 prefix > 128")]
    [InlineData("2001:db8::/300", "IPv6 prefix > 255 — byte.TryParse fails")]
    // Non-digit or signed prefix characters
    [InlineData("10.0.0.0/-1", "negative prefix")]
    [InlineData("10.0.0.0/+8", "signed positive prefix")]
    [InlineData("10.0.0.0/ 8", "space in prefix")]
    // Non-canonical address forms
    [InlineData("010.0.0.0/8", "leading zero in octet")]
    [InlineData("0x0A.0.0.0/8", "hex octet")]
    [InlineData("10.0/8", "partial IPv4 address")]
    [InlineData("1.2.3/24", "3-part IPv4 address")]
    // Structural problems
    [InlineData("not-an-ip/8", "garbage address")]
    [InlineData("/8", "empty address")]
    [InlineData("10.0.0.0/", "empty prefix")]
    [InlineData("10.0.0.0", "no slash")]
    [InlineData("10.0.0.0/8/8", "double slash — prefix part '8/8' contains non-digit")]
    // Zone IDs
    [InlineData("fe80::1%eth0/64", "IPv6 zone ID")]
    [InlineData("fe80::1%1/64", "IPv6 zone ID (numeric)")]
    // Bracketed IPv6 forms — Rust rejects these; server must match
    [InlineData("[::1]/128", "bracketed IPv6 — Rust rejects bracketed form")]
    [InlineData("[2001:db8::]/32", "bracketed IPv6 prefix")]
    // Whitespace
    [InlineData(" 10.0.0.0/8", "leading whitespace")]
    // Host bits set (IPv6)
    [InlineData("2001:db8::1/32", "IPv6 host bits set")]
    // IPv4-mapped IPv6 — SDK rejects these (test ipv4_mapped_ipv6_is_invalid); the mapped form is
    // ambiguous with the native IPv4 range
    [InlineData("::ffff:10.0.0.0/104", "IPv4-mapped IPv6")]
    [InlineData("::ffff:1.2.3.4/128", "IPv4-mapped IPv6 single host")]
    public void IsValid_InvalidCidrs_ReturnsFalse(string cidr, string reason)
    {
        // reason is only for readability in the test output
        Assert.False(CidrValidator.IsValid(cidr), $"Expected '{cidr}' to be invalid ({reason})");
    }

    [Fact]
    public void IsValid_Null_ReturnsFalse()
    {
        // JSON binding can produce null list entries; the validator must not throw on them.
        Assert.False(CidrValidator.IsValid(null));
    }
}
