using System.Security.Claims;
using Bit.Core.Billing.Licenses.Models;

namespace Bit.Core.Billing.Licenses.Services;

public interface ILicenseClaimsFactory<in T>
{
    Task<List<Claim>> GenerateClaims(T entity, LicenseContext licenseContext);
}
