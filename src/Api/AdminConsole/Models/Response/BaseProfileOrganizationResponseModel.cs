using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Response;

/// <summary>
/// Contains organization properties for both OrganizationUsers and ProviderUsers.
/// Any organization properties in sync data should be added to this class so they are populated for both
/// members and providers.
/// </summary>
public abstract class BaseProfileOrganizationResponseModel : ResponseModel
{
    protected BaseProfileOrganizationResponseModel(
        string type, IProfileOrganizationDetails organizationDetails) : base(type)
    {
        Id = organizationDetails.OrganizationId;
        UserId = organizationDetails.UserId;
        Name = organizationDetails.Name;
        Enabled = organizationDetails.Enabled;
        Identifier = organizationDetails.Identifier;
        ProductTierType = organizationDetails.PlanType.GetProductTier();
        UsePolicies = organizationDetails.UsePolicies;
        UseSso = organizationDetails.UseSso;
        UseKeyConnector = organizationDetails.UseKeyConnector;
        UseScim = organizationDetails.UseScim;
        UseGroups = organizationDetails.UseGroups;
        UseDirectory = organizationDetails.UseDirectory;
        UseEvents = organizationDetails.UseEvents;
        UseTotp = organizationDetails.UseTotp;
        Use2fa = organizationDetails.Use2fa;
        UseApi = organizationDetails.UseApi;
        UseResetPassword = organizationDetails.UseResetPassword;
        UsersGetPremium = organizationDetails.UsersGetPremium;
        UseCustomPermissions = organizationDetails.UseCustomPermissions;
        UseActivateAutofillPolicy = organizationDetails.PlanType.GetProductTier() == ProductTierType.Enterprise;
        UseRiskInsights = organizationDetails.UseRiskInsights;
        UseOrganizationDomains = organizationDetails.UseOrganizationDomains;
        UseAdminSponsoredFamilies = organizationDetails.UseAdminSponsoredFamilies;
        UseAutomaticUserConfirmation = organizationDetails.UseAutomaticUserConfirmation;
        UseSecretsManager = organizationDetails.UseSecretsManager;
        UsePhishingBlocker = organizationDetails.UsePhishingBlocker;
        UsePasswordManager = organizationDetails.UsePasswordManager;
        SelfHost = organizationDetails.SelfHost;
        Seats = organizationDetails.Seats;
        MaxCollections = organizationDetails.MaxCollections;
        MaxStorageGb = organizationDetails.MaxStorageGb;
        Key = organizationDetails.Key;
        HasPublicAndPrivateKeys = organizationDetails.PublicKey != null && organizationDetails.PrivateKey != null;
        SsoBound = !string.IsNullOrWhiteSpace(organizationDetails.SsoExternalId);
        ResetPasswordEnrolled = !string.IsNullOrWhiteSpace(organizationDetails.ResetPasswordKey);
        ProviderId = organizationDetails.ProviderId;
        ProviderName = organizationDetails.ProviderName;
        ProviderType = organizationDetails.ProviderType;
        LimitCollectionCreation = organizationDetails.LimitCollectionCreation;
        LimitCollectionDeletion = organizationDetails.LimitCollectionDeletion;
        LimitItemDeletion = organizationDetails.LimitItemDeletion;
        AllowAdminAccessToAllCollectionItems = organizationDetails.AllowAdminAccessToAllCollectionItems;
        SsoEnabled = organizationDetails.SsoEnabled ?? false;
        if (organizationDetails.SsoConfig != null)
        {
            var ssoConfigData = SsoConfigurationData.Deserialize(organizationDetails.SsoConfig);
            KeyConnectorEnabled = ssoConfigData.MemberDecryptionType == MemberDecryptionType.KeyConnector && !string.IsNullOrEmpty(ssoConfigData.KeyConnectorUrl);
            KeyConnectorUrl = ssoConfigData.KeyConnectorUrl;
            SsoMemberDecryptionType = ssoConfigData.MemberDecryptionType;
        }
    }

    public Guid Id { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; } = null!;
    public bool Enabled { get; set; }
    public string? Identifier { get; set; }
    public ProductTierType ProductTierType { get; set; }
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
    public bool UseRiskInsights { get; set; }
    public bool UseOrganizationDomains { get; set; }
    public bool UseAdminSponsoredFamilies { get; set; }
    public bool UseAutomaticUserConfirmation { get; set; }
    public bool UsePhishingBlocker { get; set; }
    public bool SelfHost { get; set; }
    public int? Seats { get; set; }
    public short? MaxCollections { get; set; }
    public short? MaxStorageGb { get; set; }
    public string? Key { get; set; }
    public bool HasPublicAndPrivateKeys { get; set; }
    public bool SsoBound { get; set; }
    public bool ResetPasswordEnrolled { get; set; }
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    public bool LimitItemDeletion { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }
    public Guid? ProviderId { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string? ProviderName { get; set; }
    public ProviderType? ProviderType { get; set; }
    public bool SsoEnabled { get; set; }
    public bool KeyConnectorEnabled { get; set; }
    public string? KeyConnectorUrl { get; set; }
    public MemberDecryptionType? SsoMemberDecryptionType { get; set; }
    public bool AccessSecretsManager { get; set; }
    public Guid? UserId { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions? Permissions { get; set; }
}
