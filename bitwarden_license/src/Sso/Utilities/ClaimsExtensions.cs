using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Bit.Sso.Utilities;

public static class ClaimsExtensions
{
    private static readonly Regex _normalizeTextRegEx =
        new Regex(@"[^a-zA-Z]", RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public static string GetFirstMatch(this IEnumerable<Claim> claims, params string[] possibleNames)
    {
        var normalizedClaims = claims.Select(c => (Normalize(c.Type), c.Value)).ToList();

        // Order of prescendence is by passed in names
        foreach (var name in possibleNames.Select(Normalize))
        {
            // Second by order of claims (find claim by name)
            foreach (var claim in normalizedClaims)
            {
                if (Equals(claim.Item1, name))
                {
                    return claim.Value;
                }
            }
        }
        return null;
    }

    private static bool Equals(string text, string compare)
    {
        return text == compare ||
            (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(compare)) ||
            string.Equals(Normalize(text), compare, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
        return _normalizeTextRegEx.Replace(text, string.Empty);
    }
}
