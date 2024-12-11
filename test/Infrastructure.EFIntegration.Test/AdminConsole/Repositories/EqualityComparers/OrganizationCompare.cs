using System.Diagnostics.CodeAnalysis;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class OrganizationCompare : IEqualityComparer<Organization>
{
    public bool Equals(Organization x, Organization y)
    {
        var a = x.ExpirationDate.ToString();
        var b = y.ExpirationDate.ToString();
        return x.Identifier.Equals(y.Identifier)
            && x.Name.Equals(y.Name)
            && x.BusinessName.Equals(y.BusinessName)
            && x.BusinessAddress1.Equals(y.BusinessAddress1)
            && x.BusinessAddress2.Equals(y.BusinessAddress2)
            && x.BusinessAddress3.Equals(y.BusinessAddress3)
            && x.BusinessCountry.Equals(y.BusinessCountry)
            && x.BusinessTaxNumber.Equals(y.BusinessTaxNumber)
            && x.BillingEmail.Equals(y.BillingEmail)
            && x.Plan.Equals(y.Plan)
            && x.PlanType.Equals(y.PlanType)
            && x.Seats.Equals(y.Seats)
            && x.MaxCollections.Equals(y.MaxCollections)
            && x.UsePolicies.Equals(y.UsePolicies)
            && x.UseSso.Equals(y.UseSso)
            && x.UseKeyConnector.Equals(y.UseKeyConnector)
            && x.UseScim.Equals(y.UseScim)
            && x.UseGroups.Equals(y.UseGroups)
            && x.UseDirectory.Equals(y.UseDirectory)
            && x.UseEvents.Equals(y.UseEvents)
            && x.UseTotp.Equals(y.UseTotp)
            && x.Use2fa.Equals(y.Use2fa)
            && x.UseApi.Equals(y.UseApi)
            && x.SelfHost.Equals(y.SelfHost)
            && x.UsersGetPremium.Equals(y.UsersGetPremium)
            && x.UseCustomPermissions.Equals(y.UseCustomPermissions)
            && x.Storage.Equals(y.Storage)
            && x.MaxStorageGb.Equals(y.MaxStorageGb)
            && x.Gateway.Equals(y.Gateway)
            && x.GatewayCustomerId.Equals(y.GatewayCustomerId)
            && x.GatewaySubscriptionId.Equals(y.GatewaySubscriptionId)
            && x.ReferenceData.Equals(y.ReferenceData)
            && x.Enabled.Equals(y.Enabled)
            && x.LicenseKey.Equals(y.LicenseKey)
            && x.TwoFactorProviders.Equals(y.TwoFactorProviders)
            && x.ExpirationDate.ToString().Equals(y.ExpirationDate.ToString());
    }

    public int GetHashCode([DisallowNull] Organization obj)
    {
        return base.GetHashCode();
    }
}
