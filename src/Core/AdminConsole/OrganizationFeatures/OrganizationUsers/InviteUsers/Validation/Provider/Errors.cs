using Bit.Core.AdminConsole.Errors;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;

public record ProviderBillableSeatLimitError(ProviderDto InvalidRequest) : Error<ProviderDto>(Code, InvalidRequest)
{
    public const string Code = "Seat limit has been reached. Please contact your provider to add more seats.";
}

public record ProviderResellerSeatLimitError(ProviderDto InvalidRequest) : Error<ProviderDto>(Code, InvalidRequest)
{
    public const string Code = "Seat limit has been reached. Contact your provider to purchase additional seats.";
}
