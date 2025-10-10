using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Models.Data.Provider;

public class ProviderUserOrganizationDetails : IProfileOrganizationDetails
{
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; } = null!;
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
    public bool SelfHost { get; set; }
    public bool UsersGetPremium { get; set; }
    public bool UseCustomPermissions { get; set; }
    public bool UseSecretsManager { get; set; }
    public bool UsePasswordManager { get; set; }
    public int? Seats { get; set; }
    public short? MaxCollections { get; set; }
    public short? MaxStorageGb { get; set; }
    public string? Key { get; set; }
    public ProviderUserStatusType Status { get; set; }
    public ProviderUserType Type { get; set; }
    public bool Enabled { get; set; }
    public string? Identifier { get; set; }
    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
    public Guid? ProviderId { get; set; }
    public Guid? ProviderUserId { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string? ProviderName { get; set; }
    public PlanType PlanType { get; set; }
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    public bool LimitItemDeletion { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }
    public bool UseRiskInsights { get; set; }
    public bool UseOrganizationDomains { get; set; }
    public bool UseAdminSponsoredFamilies { get; set; }
    public ProviderType? ProviderType { get; set; }
    public bool? SsoEnabled { get; set; }
    public string? SsoConfig { get; set; }
    public string? SsoExternalId { get; set; }
    public string? Permissions { get; set; }
    public string? ResetPasswordKey { get; set; }
}
