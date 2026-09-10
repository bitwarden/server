namespace Bit.Sso.Exceptions;

/// <summary>
/// Thrown when SSO authentication is refused because the target organization has
/// no available seats and cannot autoscale — either the instance is self-hosted
/// (autoscale unavailable) or a cloud autoscale attempt failed (e.g. billing
/// gateway rejected the growth, or the org is already at <c>MaxAutoscaleSeats</c>).
///
/// The user cannot resolve this on their own; an org admin must free up a seat
/// or raise the cap.
/// </summary>
public class SsoAuthnNoSeatsAvailableException : Exception
{
    public SsoAuthnNoSeatsAvailableException()
        : base("SSO refused: no seats available on target organization and autoscale did not grow the cap.")
    {
    }
}
