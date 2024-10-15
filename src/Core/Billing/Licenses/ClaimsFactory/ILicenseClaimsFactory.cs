using System.Security.Claims;

namespace Bit.Core.Billing.Licenses.ClaimsFactory;

public interface ILicenseClaimsFactory<TContext>
{
    Task<IEnumerable<Claim>> GenerateClaimsAsync(TContext context);
}
