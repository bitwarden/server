namespace Bit.Core.Entities;

public class SelfHostedOrganizationDetails : Organization
{
    public int OccupiedSeatCount { get; set; }
    public int CollectionCount { get; set; }
    public int GroupCount { get; set; }
    public IEnumerable<OrganizationUser> OrganizationUsers { get; set; }
    public IEnumerable<Policy> Policies { get; set; }
    public SsoConfig SsoConfig { get; set; }
    public IEnumerable<OrganizationConnection> ScimConnections { get; set; }

    public Organization ToOrganization()
    {
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
        };
    }
}
