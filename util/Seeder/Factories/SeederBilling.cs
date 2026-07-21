using System.Security.Cryptography;
using System.Text;

namespace Bit.Seeder.Factories;

internal static class SeederBilling
{
    /// <summary>
    /// Derives a deterministic, non-deliverable billing email from a domain.
    /// Always applied regardless of mangle flag — seeded billing emails must never be deliverable.
    /// </summary>
    internal static string DeriveBillingEmail(string domain)
    {
        var hash = DeriveShortHash(domain);
        return $"billing{hash}@{hash}.{domain}";
    }

    /// <summary>
    /// Derives a deterministic 8-char hex string from a domain.
    /// </summary>
    private static string DeriveShortHash(string domain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(domain));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }
}
