using Bit.Core.AdminConsole.Utilities.Errors;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;

public record ProviderBillableSeatLimitError(InviteOrganizationProvider InvalidRequest) : Error<InviteOrganizationProvider>(GetErrorMessage(InvalidRequest), InvalidRequest)
{
    private static string GetErrorMessage(InviteOrganizationProvider invalidRequest) =>
        string.Format(Code, invalidRequest.Seats);

    public const string Code = "Seat limit of {0} has been reached. Contact your provider to purchase additional seats.";
}

public record ProviderResellerSeatLimitError(InviteOrganizationProvider InvalidRequest) : Error<InviteOrganizationProvider>(GetErrorMessage(InvalidRequest), InvalidRequest)
{
    private static string GetErrorMessage(InviteOrganizationProvider invalidRequest) =>
        string.Format(Code, invalidRequest.Seats);

    public const string Code = "Seat limit of {0} has been reached. Contact your provider to purchase additional seats.";
}
