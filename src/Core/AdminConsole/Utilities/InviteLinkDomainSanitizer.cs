namespace Bit.Core.AdminConsole.Utilities;

internal static class InviteLinkDomainSanitizer
{
    /// <summary>
    /// Normalizes domains to lowercase and removes blank entries.
    /// </summary>
    internal static List<string> SanitizeDomains(IEnumerable<string>? domains) =>
        domains?
            .Select(d => d?.Trim().ToLowerInvariant())
            .Where(d => !string.IsNullOrEmpty(d))
            .Cast<string>()
            .ToList() ?? [];
}
