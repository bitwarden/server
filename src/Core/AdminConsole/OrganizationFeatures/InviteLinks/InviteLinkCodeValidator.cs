using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public static class InviteLinkCodeValidator
{
    /// <summary>Constant-time code comparison; returns false for null or empty input.</summary>
    public static bool CodesMatch(string? providedCode, string? storedCode)
    {
        if (string.IsNullOrEmpty(providedCode) || string.IsNullOrEmpty(storedCode))
        {
            return false;
        }

        return CoreHelpers.FixedTimeEquals(providedCode, storedCode);
    }
}
