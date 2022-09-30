namespace Bit.Core.Settings;

public interface IGlobalSettings
{
    // This interface exists for testing. Add settings here as needed for testing
    bool SelfHosted { get; set; }
    bool EnableCloudCommunication { get; set; }
    string LicenseDirectory { get; set; }
    string LicenseCertificatePassword { get; set; }
    int OrganizationInviteExpirationHours { get; set; }
    bool DisableUserRegistration { get; set; }
    IInstallationSettings Installation { get; set; }
    IFileStorageSettings Attachment { get; set; }
    IConnectionStringSettings Storage { get; set; }
    IBaseServiceUriSettings BaseServiceUri { get; set; }
    ITwoFactorAuthSettings TwoFactorAuth { get; set; }
    ISsoSettings Sso { get; set; }
    ILogLevelSettings MinLogLevel { get; set; }
    IPasswordlessAuthSettings PasswordlessAuth { get; set; }
}
