using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Extensions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public static class InvitingUserOrganizationProviderValidation
{
    public static ValidationResult<ProviderDto> Validate(ProviderDto provider)
    {
        if (provider is { Enabled: true })
        {
            if (provider.IsBillable())
            {
                return new Invalid<ProviderDto>(InviteUserValidationErrorMessages.ProviderBillableSeatLimitError);
            }

            if (provider.Type == ProviderType.Reseller)
            {
                return new Invalid<ProviderDto>(InviteUserValidationErrorMessages.ProviderResellerSeatLimitError);
            }
        }

        return new Valid<ProviderDto>(provider);
    }
}

public record ProviderDto
{
    public Guid ProviderId { get; init; }
    public ProviderType Type { get; init; }
    public ProviderStatusType Status { get; init; }
    public bool Enabled { get; init; }

    public static ProviderDto FromProviderEntity(Provider provider)
    {
        return new ProviderDto { ProviderId = provider.Id, Type = provider.Type, Status = provider.Status, Enabled = provider.Enabled };
    }
}
