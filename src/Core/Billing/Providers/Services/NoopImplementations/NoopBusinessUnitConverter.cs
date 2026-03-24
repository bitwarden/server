using Bit.Core.AdminConsole.Entities;
using OneOf;

namespace Bit.Core.Billing.Providers.Services.NoopImplementations;

public class NoopBusinessUnitConverter : IBusinessUnitConverter
{
    public Task<Guid> FinalizeConversion(
        Organization organization,
        Guid userId,
        string token,
        string providerKey,
        string organizationKey) => throw new NotImplementedException();

    public Task<OneOf<Guid, List<string>>> InitiateConversion(Organization organization, string providerAdminEmail) => throw new NotImplementedException();

    public Task ResendConversionInvite(Organization organization, string providerAdminEmail) => throw new NotImplementedException();

    public Task ResetConversion(Organization organization, string providerAdminEmail) => throw new NotImplementedException();
}
