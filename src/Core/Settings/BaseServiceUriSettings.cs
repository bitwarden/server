namespace Bit.Core.Settings;

public class BaseServiceUriSettings : IBaseServiceUriSettings
{
    private readonly GlobalSettings _globalSettings;

    private string _api;
    private string _identity;
    private string _admin;
    private string _notifications;
    private string _sso;
    private string _scim;
    private string _internalApi;
    private string _internalIdentity;
    private string _internalAdmin;
    private string _internalNotifications;
    private string _internalSso;
    private string _internalVault;
    private string _internalScim;
    private string _internalBilling;

    public BaseServiceUriSettings(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public string CloudRegion { get; set; }
    public string Vault { get; set; }
    public string VaultWithHash => $"{Vault}/#";

    public string VaultWithHashAndSecretManagerProduct => $"{Vault}/#/sm";

    public string Api
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildExternalUri(_api, "api");
        set => _api = value;
    }
    public string Identity
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildExternalUri(_identity, "identity");
        set => _identity = value;
    }
    public string Admin
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildExternalUri(_admin, "admin");
        set => _admin = value;
    }
    public string Notifications
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildExternalUri(_notifications, "notifications");
        set => _notifications = value;
    }
    public string Sso
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildExternalUri(_sso, "sso");
        set => _sso = value;
    }
    public string Scim
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildExternalUri(_scim, "scim");
        set => _scim = value;
    }

    public string InternalNotifications
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildInternalUri(_internalNotifications, "notifications");
        set => _internalNotifications = value;
    }
    public string InternalAdmin
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildInternalUri(_internalAdmin, "admin");
        set => _internalAdmin = value;
    }
    public string InternalIdentity
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildInternalUri(_internalIdentity, "identity");
        set => _internalIdentity = value;
    }
    public string InternalApi
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildInternalUri(_internalApi, "api");
        set => _internalApi = value;
    }
    public string InternalVault
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildInternalUri(_internalVault, "web");
        set => _internalVault = value;
    }
    public string InternalSso
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildInternalUri(_internalSso, "sso");
        set => _internalSso = value;
    }
    public string InternalScim
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildInternalUri(_scim, "scim");
        set => _internalScim = value;
    }

    public string InternalBilling
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildInternalUri(_internalBilling, "billing");
        set => _internalBilling = value;
    }
}
