using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response;

public class ProfileOrganizationResponseModel : ResponseModel
{
    public ProfileOrganizationResponseModel(string str) : base(str) { }

    public ProfileOrganizationResponseModel(OrganizationUserOrganizationDetails organization) : this("profileOrganization")
    {
        Id = organization.OrganizationId.ToString();
        Name = organization.Name;
        UsePolicies = organization.UsePolicies;
        UseSso = organization.UseSso;
        UseKeyConnector = organization.UseKeyConnector;
        UseScim = organization.UseScim;
        UseGroups = organization.UseGroups;
        UseDirectory = organization.UseDirectory;
        UseEvents = organization.UseEvents;
        UseTotp = organization.UseTotp;
        Use2fa = organization.Use2fa;
        UseApi = organization.UseApi;
        UseResetPassword = organization.UseResetPassword;
        UsersGetPremium = organization.UsersGetPremium;
        SelfHost = organization.SelfHost;
        Seats = organization.Seats;
        MaxCollections = organization.MaxCollections;
        MaxStorageGb = organization.MaxStorageGb;
        Key = organization.Key;
        HasPublicAndPrivateKeys = organization.PublicKey != null && organization.PrivateKey != null;
        Status = organization.Status;
        Type = organization.Type;
        Enabled = organization.Enabled;
        SsoBound = !string.IsNullOrWhiteSpace(organization.SsoExternalId);
        Identifier = organization.Identifier;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(organization.Permissions);
        ResetPasswordEnrolled = organization.ResetPasswordKey != null;
        UserId = organization.UserId?.ToString();
        ProviderId = organization.ProviderId?.ToString();
        ProviderName = organization.ProviderName;
        FamilySponsorshipFriendlyName = organization.FamilySponsorshipFriendlyName;
        FamilySponsorshipAvailable = FamilySponsorshipFriendlyName == null &&
            StaticStore.GetSponsoredPlan(PlanSponsorshipType.FamiliesForEnterprise)
            .UsersCanSponsor(organization);
        PlanProductType = StaticStore.GetPlan(organization.PlanType).Product;
        FamilySponsorshipLastSyncDate = organization.FamilySponsorshipLastSyncDate;
        FamilySponsorshipToDelete = organization.FamilySponsorshipToDelete;
        FamilySponsorshipValidUntil = organization.FamilySponsorshipValidUntil;

        if (organization.SsoConfig != null)
        {
            var ssoConfigData = SsoConfigurationData.Deserialize(organization.SsoConfig);
            KeyConnectorEnabled = ssoConfigData.KeyConnectorEnabled && !string.IsNullOrEmpty(ssoConfigData.KeyConnectorUrl);
            KeyConnectorUrl = ssoConfigData.KeyConnectorUrl;
        }
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public bool UsePolicies { get; set; }
    public bool UseSso { get; set; }
    public bool UseKeyConnector { get; set; }
    public bool UseScim { get; set; }
    public bool UseGroups { get; set; }
    public bool UseDirectory { get; set; }
    public bool UseEvents { get; set; }
    public bool UseTotp { get; set; }
    public bool Use2fa { get; set; }
    public bool UseApi { get; set; }
    public bool UseResetPassword { get; set; }
    public bool UsersGetPremium { get; set; }
    public bool SelfHost { get; set; }
    public int? Seats { get; set; }
    public short? MaxCollections { get; set; }
    public short? MaxStorageGb { get; set; }
    public string Key { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public OrganizationUserType Type { get; set; }
    public bool Enabled { get; set; }
    public bool SsoBound { get; set; }
    public string Identifier { get; set; }
    public Permissions Permissions { get; set; }
    public bool ResetPasswordEnrolled { get; set; }
    public string UserId { get; set; }
    public bool HasPublicAndPrivateKeys { get; set; }
    public string ProviderId { get; set; }
    public string ProviderName { get; set; }
    public string FamilySponsorshipFriendlyName { get; set; }
    public bool FamilySponsorshipAvailable { get; set; }
    public ProductType PlanProductType { get; set; }
    public bool KeyConnectorEnabled { get; set; }
    public string KeyConnectorUrl { get; set; }
    public DateTime? FamilySponsorshipLastSyncDate { get; set; }
    public DateTime? FamilySponsorshipValidUntil { get; set; }
    public bool? FamilySponsorshipToDelete { get; set; }
}
