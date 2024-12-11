namespace Bit.Core.Settings;

public interface IBaseServiceUriSettings
{
    string CloudRegion { get; set; }
    string Vault { get; set; }
    string VaultWithHash { get; }
    string VaultWithHashAndSecretManagerProduct { get; }
    string Api { get; set; }
    public string Identity { get; set; }
    public string Admin { get; set; }
    public string Notifications { get; set; }
    public string Sso { get; set; }
    public string Scim { get; set; }
    public string InternalNotifications { get; set; }
    public string InternalAdmin { get; set; }
    public string InternalIdentity { get; set; }
    public string InternalApi { get; set; }
    public string InternalVault { get; set; }
    public string InternalSso { get; set; }
    public string InternalScim { get; set; }
    public string InternalBilling { get; set; }
}
