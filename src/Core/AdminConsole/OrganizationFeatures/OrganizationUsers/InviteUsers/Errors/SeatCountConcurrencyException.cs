namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;

public class SeatCountConcurrencyException : Exception
{
    public SeatCountConcurrencyException()
        : base("The organization's seat count was modified by another request. Please retry.")
    { }
}
