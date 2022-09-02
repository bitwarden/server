namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserOrganizationDetails
{
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
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
    public bool SelfHost { get; set; }
    public bool UsersGetPremium { get; set; }
    public int? Seats { get; set; }
    public short? MaxCollections { get; set; }
    public short? MaxStorageGb { get; set; }
    public string Key { get; set; }
    public Enums.OrganizationUserStatusType Status { get; set; }
    public Enums.OrganizationUserType Type { get; set; }
    public bool Enabled { get; set; }
    public Enums.PlanType PlanType { get; set; }
    public string SsoExternalId { get; set; }
    public string Identifier { get; set; }
    public string Permissions { get; set; }
    public string ResetPasswordKey { get; set; }
    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }
    public Guid? ProviderId { get; set; }
    public string ProviderName { get; set; }
    public string FamilySponsorshipFriendlyName { get; set; }
    public string SsoConfig { get; set; }
    public DateTime? FamilySponsorshipLastSyncDate { get; set; }
    public DateTime? FamilySponsorshipValidUntil { get; set; }
    public bool? FamilySponsorshipToDelete { get; set; }
}
