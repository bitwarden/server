using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// Validates CIDR range strings using the same rules as the Bitwarden SDK's <c>is_valid_cidr</c>:
/// canonical address form, a plain decimal prefix in the valid range for the address family, and
/// no host bits set.
///
/// <para><b>IPv4</b>: the address must round-trip through <see cref="IPAddress.ToString"/> unchanged
/// (rejects leading-zero octets, hex octets, and partial addresses).</para>
///
/// <para><b>IPv6</b>: any RFC-4291 textual form that <see cref="IPAddress.TryParse(string?,out IPAddress?)"/> accepts is
/// valid as long as it contains no zone ID (<c>%</c>), no bracketed form (<c>[</c>/<c>]</c>), and
/// is not an IPv4-mapped address (<c>::ffff:a.b.c.d</c>) — the SDK rejects mapped addresses as
/// ambiguous with the native IPv4 range. Leading zeros in hextets and uncompressed forms such as
/// <c>2001:0db8::/32</c> or <c>2001:db8:0:0:0:0:0:0/32</c> are accepted — matching Rust's
/// <c>Ipv6Addr::from_str</c>, which compares by value.</para>
/// </summary>
internal static class CidrValidator
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a valid CIDR range:
    /// <c>address/prefix</c> where the address parses for its family (see class-level rules),
    /// the prefix is a plain decimal integer in the valid range (0–32 for IPv4, 0–128 for IPv6),
    /// and no host bits are set (e.g. <c>10.0.0.0/8</c> is valid; <c>10.0.0.1/8</c> is not).
    /// A <see langword="null"/> value returns <see langword="false"/>.
    /// </summary>
    public static bool IsValid(string? value)
    {
        // JSON binding can produce null list entries; treat them as invalid rather than throwing.
        if (value is null)
        {
            return false;
        }

        // Split on the first '/' only — mirrors Rust's split_once('/').
        var slashIndex = value.IndexOf('/');
        if (slashIndex < 0)
        {
            return false;
        }

        var addrPart = value[..slashIndex];
        var prefixPart = value[(slashIndex + 1)..];

        // Empty address or empty prefix are invalid.
        if (addrPart.Length == 0 || prefixPart.Length == 0)
        {
            return false;
        }

        // Parse prefix. byte covers 0–255, sufficient for both IPv4 (max 32) and IPv6 (max 128).
        // NumberStyles.None rejects signs, whitespace, and any non-digit (e.g. the "8/8" prefix
        // part of "10.0.0.0/8/8") while still accepting leading zeros such as "08" — matching the
        // Rust SDK's u8 parsing (test prefix_with_leading_zero_is_valid).
        if (!byte.TryParse(prefixPart, NumberStyles.None, CultureInfo.InvariantCulture, out var prefix))
        {
            return false;
        }

        // Reject IPv6 zone IDs (e.g. "fe80::1%eth0") before calling TryParse. Rust's
        // Ipv6Addr::from_str rejects zone IDs; the server must match.
        if (addrPart.Contains('%'))
        {
            return false;
        }

        // Reject bracketed IPv6 forms (e.g. "[::1]") — Rust rejects those too.
        if (addrPart.Contains('[') || addrPart.Contains(']'))
        {
            return false;
        }

        if (!IPAddress.TryParse(addrPart, out var ip))
        {
            return false;
        }

        return ip.AddressFamily switch
        {
            // IPv4: round-trip through ToString() to reject non-canonical forms such as
            // leading-zero octets ("010.0.0.0") and partial addresses ("1.2.3"). IPAddress.Parse
            // accepts these on some platforms; the round-tripped string differs from the input.
            AddressFamily.InterNetwork =>
                string.Equals(ip.ToString(), addrPart, StringComparison.OrdinalIgnoreCase)
                && prefix <= 32
                && NoHostBits(ip.GetAddressBytes(), 32 - prefix),

            // IPv6: skip the round-trip check. Rust's Ipv6Addr::from_str accepts any RFC-4291
            // textual form (leading zeros in hextets, uncompressed, etc.) and compares by value,
            // so "2001:0db8::/32" and "2001:db8:0:0:0:0:0:0/32" are valid there. Requiring a
            // round-trip here would make the server reject inputs the SDK accepts, causing a
            // client-passes-then-server-400 failure mode. Zone IDs and bracketed forms are already
            // rejected above before reaching TryParse. IPv4-mapped addresses (::ffff:a.b.c.d) are
            // rejected to match the SDK (to_ipv4_mapped().is_none(), test
            // ipv4_mapped_ipv6_is_invalid) — the mapped form is ambiguous with the native IPv4
            // range, and accepting it here would let rules exist that SDK clients refuse to edit.
            AddressFamily.InterNetworkV6 =>
                !ip.IsIPv4MappedToIPv6
                && prefix <= 128
                && NoHostBits(ip.GetAddressBytes(), 128 - prefix),

            _ => false,
        };
    }

    /// <summary>
    /// Returns <see langword="true"/> when the low <paramref name="hostBits"/> bits of the
    /// big-endian address (4 bytes for IPv4, 16 for IPv6) are all zero.
    /// </summary>
    private static bool NoHostBits(byte[] addressBytes, int hostBits)
    {
        // Walk from the last byte backward, checking that the low hostBits bits are all zero.
        var remaining = hostBits;
        for (var i = addressBytes.Length - 1; i >= 0 && remaining > 0; i--)
        {
            var bitsInByte = Math.Min(remaining, 8);
            var mask = (byte)((1 << bitsInByte) - 1);
            if ((addressBytes[i] & mask) != 0)
            {
                return false;
            }
            remaining -= bitsInByte;
        }
        return true;
    }
}
