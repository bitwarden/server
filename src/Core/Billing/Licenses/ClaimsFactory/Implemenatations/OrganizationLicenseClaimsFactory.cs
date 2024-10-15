using System.Security.Claims;
using Bit.Core.Billing.Licenses.OrganizationLicenses;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.Licenses.ClaimsFactory;

public class OrganizationLicenseClaimsFactory : ILicenseClaimsFactory<OrganizationLicense>
{
    public Task<IEnumerable<Claim>> GenerateClaimsAsync(OrganizationLicense context)
    {
        var claims = new List<Claim>
        {
            new(nameof(OrganizationLicense.LicenseKey), context.LicenseKey),
            new(nameof(OrganizationLicense.InstallationId), context.InstallationId.ToString()),
            new(nameof(OrganizationLicense.Id), context.Id.ToString()),
            new(nameof(OrganizationLicense.Name), context.Name),
            new(nameof(OrganizationLicense.BillingEmail), context.BillingEmail),
            new(nameof(OrganizationLicense.Enabled), context.Enabled.ToString()),
            new(nameof(OrganizationLicense.Plan), context.Plan),
            new(nameof(OrganizationLicense.PlanType), context.PlanType.ToString()),
            new(nameof(OrganizationLicense.UseGroups), context.UseGroups.ToString()),
            new(nameof(OrganizationLicense.UseDirectory), context.UseDirectory.ToString()),
            new(nameof(OrganizationLicense.UseTotp), context.UseTotp.ToString()),
            new(nameof(OrganizationLicense.SelfHost), context.SelfHost.ToString()),
            new(nameof(OrganizationLicense.Trial), context.Trial.ToString()),
            new(nameof(OrganizationLicense.UsersGetPremium), context.UsersGetPremium.ToString()),
            new(nameof(OrganizationLicense.UseEvents), context.UseEvents.ToString()),
            new(nameof(OrganizationLicense.Use2fa), context.Use2fa.ToString()),
            new(nameof(OrganizationLicense.UseApi), context.UseApi.ToString()),
            new(nameof(OrganizationLicense.UsePolicies), context.UsePolicies.ToString()),
            new(nameof(OrganizationLicense.UseSso), context.UseSso.ToString()),
            new(nameof(OrganizationLicense.UseResetPassword), context.UseResetPassword.ToString()),
            new(nameof(OrganizationLicense.UseKeyConnector), context.UseKeyConnector.ToString()),
            new(nameof(OrganizationLicense.UseScim), context.UseScim.ToString()),
            new(nameof(OrganizationLicense.UseCustomPermissions), context.UseCustomPermissions.ToString()),
            new(nameof(OrganizationLicense.UsePasswordManager), context.UsePasswordManager.ToString()),
            new(nameof(OrganizationLicense.UseSecretsManager), context.UseSecretsManager.ToString()),
            new(nameof(OrganizationLicense.SmSeats), context.SmSeats.ToString()),
            new(nameof(OrganizationLicense.SmServiceAccounts), context.SmServiceAccounts.ToString()),
            new(nameof(OrganizationLicense.LimitCollectionCreationDeletion),
                context.LimitCollectionCreationDeletion.ToString()),
            new(nameof(OrganizationLicense.AllowAdminAccessToAllCollectionItems),
                context.AllowAdminAccessToAllCollectionItems.ToString()),
            new(nameof(OrganizationLicense.LicenseType), context.LicenseType.ToString()),
            new(nameof(OrganizationLicense.Issued), context.Issued.ToString()),
            new(nameof(OrganizationLicense.Refresh), context.Refresh.ToString()),
            new(nameof(OrganizationLicense.Expires), context.Expires.ToString())
        };

        if (context.BusinessName is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicense.BusinessName), context.BusinessName));
        }

        if (context.Seats is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicense.Seats), context.Seats.ToString()));
        }

        if (context.MaxCollections is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicense.MaxCollections), context.MaxCollections.ToString()));
        }

        if (context.MaxStorageGb is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicense.MaxStorageGb), context.MaxStorageGb.ToString()));
        }

        if (context.ExpirationWithoutGracePeriod is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicense.ExpirationWithoutGracePeriod),
                context.ExpirationWithoutGracePeriod.ToString()));
        }

        return Task.FromResult<IEnumerable<Claim>>(claims);
    }
}
