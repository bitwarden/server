namespace Bit.Core.Billing.Constants;

public static class TrialInitiationConstants
{
    public const int DefaultTrialLengthDays = 7;

    /// <summary>
    /// The sign-up initiation path the client sends for a trial started from a sales invitation.
    /// Must match <c>InitiationPath.SalesAssistedTrialFromAdminPortal</c> in the clients repo
    /// (<c>libs/common/src/billing/enums/initiation-path.enum.ts</c>).
    /// </summary>
    public const string SalesAssistedTrialInitiationPath = "Sales assisted trial from admin portal";
}
