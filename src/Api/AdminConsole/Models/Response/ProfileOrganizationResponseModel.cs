// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Response;

public class ProfileOrganizationResponseModel : ResponseModel
{
    public ProfileOrganizationResponseModel(string str) : base(str) { }

    public ProfileOrganizationResponseModel(
        OrganizationUserOrganizationDetails organization,
        IEnumerable<Guid> organizationIdsClaimingUser)
        : this("profileOrganization")
    {
        Id = organization.OrganizationId;
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
        UseSecretsManager = organization.UseSecretsManager;
        UsePasswordManager = organization.UsePasswordManager;
        UsersGetPremium = organization.UsersGetPremium;
        UseCustomPermissions = organization.UseCustomPermissions;
        UseActivateAutofillPolicy = organization.PlanType.GetProductTier() == ProductTierType.Enterprise;
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
        ResetPasswordEnrolled = !string.IsNullOrWhiteSpace(organization.ResetPasswordKey);
        UserId = organization.UserId;
        OrganizationUserId = organization.OrganizationUserId;
        ProviderId = organization.ProviderId;
        ProviderName = organization.ProviderName;
        ProviderType = organization.ProviderType;
        FamilySponsorshipFriendlyName = organization.FamilySponsorshipFriendlyName;
        IsAdminInitiated = organization.IsAdminInitiated ?? false;
        FamilySponsorshipAvailable = (FamilySponsorshipFriendlyName == null || IsAdminInitiated) &&
            StaticStore.GetSponsoredPlan(PlanSponsorshipType.FamiliesForEnterprise)
            .UsersCanSponsor(organization);
        ProductTierType = organization.PlanType.GetProductTier();
        FamilySponsorshipLastSyncDate = organization.FamilySponsorshipLastSyncDate;
        FamilySponsorshipToDelete = organization.FamilySponsorshipToDelete;
        FamilySponsorshipValidUntil = organization.FamilySponsorshipValidUntil;
        AccessSecretsManager = organization.AccessSecretsManager;
        LimitCollectionCreation = organization.LimitCollectionCreation;
        LimitCollectionDeletion = organization.LimitCollectionDeletion;
        LimitItemDeletion = organization.LimitItemDeletion;
        AllowAdminAccessToAllCollectionItems = organization.AllowAdminAccessToAllCollectionItems;
        UserIsClaimedByOrganization = organizationIdsClaimingUser.Contains(organization.OrganizationId);
        UseRiskInsights = organization.UseRiskInsights;
        UseOrganizationDomains = organization.UseOrganizationDomains;
        UseAdminSponsoredFamilies = organization.UseAdminSponsoredFamilies;
        SsoEnabled = organization.SsoEnabled ?? false;

        if (organization.SsoConfig != null)
        {
            var ssoConfigData = SsoConfigurationData.Deserialize(organization.SsoConfig);
            KeyConnectorEnabled = ssoConfigData.MemberDecryptionType == MemberDecryptionType.KeyConnector && !string.IsNullOrEmpty(ssoConfigData.KeyConnectorUrl);
            KeyConnectorUrl = ssoConfigData.KeyConnectorUrl;
            SsoMemberDecryptionType = ssoConfigData.MemberDecryptionType;
        }
    }

    public Guid Id { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
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
    public bool UseSecretsManager { get; set; }
    public bool UsePasswordManager { get; set; }
    public bool UsersGetPremium { get; set; }
    public bool UseCustomPermissions { get; set; }
    public bool UseActivateAutofillPolicy { get; set; }
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
    public Guid? UserId { get; set; }
    public Guid OrganizationUserId { get; set; }
    public bool HasPublicAndPrivateKeys { get; set; }
    public Guid? ProviderId { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string ProviderName { get; set; }
    public ProviderType? ProviderType { get; set; }
    public string FamilySponsorshipFriendlyName { get; set; }
    public bool FamilySponsorshipAvailable { get; set; }
    public ProductTierType ProductTierType { get; set; }
    public bool KeyConnectorEnabled { get; set; }
    public string KeyConnectorUrl { get; set; }
    public DateTime? FamilySponsorshipLastSyncDate { get; set; }
    public DateTime? FamilySponsorshipValidUntil { get; set; }
    public bool? FamilySponsorshipToDelete { get; set; }
    public bool AccessSecretsManager { get; set; }
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    public bool LimitItemDeletion { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }
    /// <summary>
    /// Obsolete.
    /// See <see cref="UserIsClaimedByOrganization"/>
    /// </summary>
    [Obsolete("Please use UserIsClaimedByOrganization instead. This property will be removed in a future version.")]
    public bool UserIsManagedByOrganization
    {
        get => UserIsClaimedByOrganization;
        set => UserIsClaimedByOrganization = value;
    }
    /// <summary>
    /// Indicates if the user is claimed by the organization.
    /// </summary>
    /// <remarks>
    /// A user is claimed by an organization if the user's email domain is verified by the organization and the user is a member.
    /// The organization must be enabled and able to have verified domains.
    /// </remarks>
    public bool UserIsClaimedByOrganization { get; set; }
    public bool UseRiskInsights { get; set; }
    public bool UseOrganizationDomains { get; set; }
    public bool UseAdminSponsoredFamilies { get; set; }
    public bool IsAdminInitiated { get; set; }
    public bool SsoEnabled { get; set; }
    public MemberDecryptionType? SsoMemberDecryptionType { get; set; }
}
