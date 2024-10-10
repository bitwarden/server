using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Models.Data.Organizations;

public class SelfHostedOrganizationDetails : Organization
{
    public int OccupiedSeatCount { get; set; }
    public int CollectionCount { get; set; }
    public int GroupCount { get; set; }
    public IEnumerable<OrganizationUser> OrganizationUsers { get; set; }
    public IEnumerable<Policy> Policies { get; set; }
    public SsoConfig SsoConfig { get; set; }
    public IEnumerable<OrganizationConnection> ScimConnections { get; set; }

    public bool CanUseLicense(OrganizationLicense license, out string exception)
    {
        if (license.Seats.HasValue && OccupiedSeatCount > license.Seats.Value)
        {
            exception = $"Your organization currently has {OccupiedSeatCount} seats filled. " +
                $"Your new license only has ({license.Seats.Value}) seats. Remove some users.";
            return false;
        }

        if (license.MaxCollections.HasValue && CollectionCount > license.MaxCollections.Value)
        {
            exception = $"Your organization currently has {CollectionCount} collections. " +
                $"Your new license allows for a maximum of ({license.MaxCollections.Value}) collections. " +
                "Remove some collections.";
            return false;
        }

        if (!license.UseGroups && UseGroups && GroupCount > 1)
        {
            exception = $"Your organization currently has {GroupCount} groups. " +
                $"Your new license does not allow for the use of groups. Remove all groups.";
            return false;
        }

        var enabledPolicyCount = Policies.Count(p => p.Enabled);
        if (!license.UsePolicies && UsePolicies && enabledPolicyCount > 0)
        {
            exception = $"Your organization currently has {enabledPolicyCount} enabled " +
                $"policies. Your new license does not allow for the use of policies. Disable all policies.";
            return false;
        }

        if (!license.UseSso && UseSso && SsoConfig is { Enabled: true })
        {
            exception = $"Your organization currently has a SSO configuration. " +
                $"Your new license does not allow for the use of SSO. Disable your SSO configuration.";
            return false;
        }

        if (!license.UseKeyConnector && UseKeyConnector && SsoConfig?.Data != null &&
            SsoConfig.GetData().MemberDecryptionType == MemberDecryptionType.KeyConnector)
        {
            exception = $"Your organization currently has Key Connector enabled. " +
                $"Your new license does not allow for the use of Key Connector. Disable your Key Connector.";
            return false;
        }

        if (!license.UseScim && UseScim && ScimConnections != null &&
            ScimConnections.Any(c => c.GetConfig<ScimConfig>() is { Enabled: true }))
        {
            exception = "Your new plan does not allow the SCIM feature. " +
                "Disable your SCIM configuration.";
            return false;
        }

        if (!license.UseCustomPermissions && UseCustomPermissions &&
            OrganizationUsers.Any(ou => ou.Type == OrganizationUserType.Custom))
        {
            exception = "Your new plan does not allow the Custom Permissions feature. " +
                "Disable your Custom Permissions configuration.";
            return false;
        }

        if (!license.UseResetPassword && UseResetPassword &&
            Policies.Any(p => p.Type == PolicyType.ResetPassword && p.Enabled))
        {
            exception = "Your new license does not allow the Password Reset feature. "
                + "Disable your Password Reset policy.";
            return false;
        }

        exception = "";
        return true;
    }

    public Organization ToOrganization()
    {
        // Any new Organization properties must be added here for them to flow through to self-hosted organizations
        return new Organization
        {
            Id = Id,
            Identifier = Identifier,
            Name = Name,
            BusinessName = BusinessName,
            BusinessAddress1 = BusinessAddress1,
            BusinessAddress2 = BusinessAddress2,
            BusinessAddress3 = BusinessAddress3,
            BusinessCountry = BusinessCountry,
            BusinessTaxNumber = BusinessTaxNumber,
            BillingEmail = BillingEmail,
            Plan = Plan,
            PlanType = PlanType,
            Seats = Seats,
            MaxCollections = MaxCollections,
            UsePolicies = UsePolicies,
            UseSso = UseSso,
            UseKeyConnector = UseKeyConnector,
            UseScim = UseScim,
            UseGroups = UseGroups,
            UseDirectory = UseDirectory,
            UseEvents = UseEvents,
            UseTotp = UseTotp,
            Use2fa = Use2fa,
            UseApi = UseApi,
            UseResetPassword = UseResetPassword,
            UseSecretsManager = UseSecretsManager,
            SelfHost = SelfHost,
            UsersGetPremium = UsersGetPremium,
            UseCustomPermissions = UseCustomPermissions,
            Storage = Storage,
            MaxStorageGb = MaxStorageGb,
            Gateway = Gateway,
            GatewayCustomerId = GatewayCustomerId,
            GatewaySubscriptionId = GatewaySubscriptionId,
            ReferenceData = ReferenceData,
            Enabled = Enabled,
            LicenseKey = LicenseKey,
            PublicKey = PublicKey,
            PrivateKey = PrivateKey,
            TwoFactorProviders = TwoFactorProviders,
            ExpirationDate = ExpirationDate,
            CreationDate = CreationDate,
            RevisionDate = RevisionDate,
            MaxAutoscaleSeats = MaxAutoscaleSeats,
            OwnersNotifiedOfAutoscaling = OwnersNotifiedOfAutoscaling,
            LimitCollectionCreation = LimitCollectionCreation,
            LimitCollectionDeletion = LimitCollectionDeletion,
            // Deprecated: https://bitwarden.atlassian.net/browse/PM-10863
            LimitCollectionCreationDeletion = LimitCollectionCreationDeletion,
            AllowAdminAccessToAllCollectionItems = AllowAdminAccessToAllCollectionItems,
            Status = Status
        };
    }
}
