﻿using System.Text.Json.Serialization;
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
/// Base class for profile organization response models that ensures all properties are fully populated.
/// This class cannot be instantiated with an empty constructor to prevent incomplete hydration.
/// </summary>
public abstract class BaseProfileOrganizationResponseModel : ResponseModel
{
    protected BaseProfileOrganizationResponseModel(string type, BaseUserOrganizationDetails organization) : base(type)
    {
        Id = organization.OrganizationId;
        Name = organization.Name;
        Enabled = organization.Enabled;
        Identifier = organization.Identifier;
        ProductTierType = organization.PlanType.GetProductTier();
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
        UseCustomPermissions = organization.UseCustomPermissions;
        UseActivateAutofillPolicy = organization.PlanType.GetProductTier() == ProductTierType.Enterprise;
        UseRiskInsights = organization.UseRiskInsights;
        UseOrganizationDomains = organization.UseOrganizationDomains;
        UseAdminSponsoredFamilies = organization.UseAdminSponsoredFamilies;
        SelfHost = organization.SelfHost;
        Seats = organization.Seats;
        MaxCollections = organization.MaxCollections;
        MaxStorageGb = organization.MaxStorageGb;
        Key = organization.Key;
        HasPublicAndPrivateKeys = organization.PublicKey != null && organization.PrivateKey != null;
        ProviderId = organization.ProviderId;
        ProviderName = organization.ProviderName;
        ProviderType = organization.ProviderType;
        LimitCollectionCreation = organization.LimitCollectionCreation;
        LimitCollectionDeletion = organization.LimitCollectionDeletion;
        LimitItemDeletion = organization.LimitItemDeletion;
        AllowAdminAccessToAllCollectionItems = organization.AllowAdminAccessToAllCollectionItems;
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
    public string? FamilySponsorshipFriendlyName { get; set; }
    public bool FamilySponsorshipAvailable { get; set; }
    public DateTime? FamilySponsorshipLastSyncDate { get; set; }
    public DateTime? FamilySponsorshipValidUntil { get; set; }
    public bool? FamilySponsorshipToDelete { get; set; }
    public bool IsAdminInitiated { get; set; }
    public bool AccessSecretsManager { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationUserId { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions? Permissions { get; set; }
    public bool UserIsClaimedByOrganization { get; set; }

    /// <summary>
    /// Obsolete property for backward compatibility
    /// </summary>
    [Obsolete("Please use UserIsClaimedByOrganization instead. This property will be removed in a future version.")]
    public bool UserIsManagedByOrganization
    {
        get => UserIsClaimedByOrganization;
        set => UserIsClaimedByOrganization = value;
    }
}
