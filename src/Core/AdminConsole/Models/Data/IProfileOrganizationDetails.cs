using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

/// <summary>
/// Interface defining common organization details properties shared between
/// regular organization users and provider organization users for profile endpoints.
/// </summary>
public interface IProfileOrganizationDetails
{
    Guid? UserId { get; set; }
    Guid OrganizationId { get; set; }
    string Name { get; set; }
    bool Enabled { get; set; }
    PlanType PlanType { get; set; }
    bool UsePolicies { get; set; }
    bool UseSso { get; set; }
    bool UseKeyConnector { get; set; }
    bool UseScim { get; set; }
    bool UseGroups { get; set; }
    bool UseDirectory { get; set; }
    bool UseEvents { get; set; }
    bool UseTotp { get; set; }
    bool Use2fa { get; set; }
    bool UseApi { get; set; }
    bool UseResetPassword { get; set; }
    bool SelfHost { get; set; }
    bool UsersGetPremium { get; set; }
    bool UseCustomPermissions { get; set; }
    bool UseSecretsManager { get; set; }
    int? Seats { get; set; }
    short? MaxCollections { get; set; }
    short? MaxStorageGb { get; set; }
    string? Identifier { get; set; }
    string? Key { get; set; }
    string? ResetPasswordKey { get; set; }
    string? PublicKey { get; set; }
    string? PrivateKey { get; set; }
    string? SsoExternalId { get; set; }
    string? Permissions { get; set; }
    Guid? ProviderId { get; set; }
    string? ProviderName { get; set; }
    ProviderType? ProviderType { get; set; }
    bool? SsoEnabled { get; set; }
    string? SsoConfig { get; set; }
    bool UsePasswordManager { get; set; }
    bool LimitCollectionCreation { get; set; }
    bool LimitCollectionDeletion { get; set; }
    bool AllowAdminAccessToAllCollectionItems { get; set; }
    bool UseRiskInsights { get; set; }
    bool LimitItemDeletion { get; set; }
    bool UseAdminSponsoredFamilies { get; set; }
    bool UseOrganizationDomains { get; set; }
}
