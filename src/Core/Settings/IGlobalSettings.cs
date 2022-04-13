using static Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Settings
{
    public interface IGlobalSettings
    {
        // This interface exists for testing. Add settings here as needed for testing
        bool SelfHosted { get; set; }
        bool EnableCloudCommunication { get; set; }
        string LicenseDirectory { get; set; }
        string LicenseCertificatePassword { get; set; }
        int OrganizationInviteExpirationHours { get; set; }
        InstallationSettings Installation { get; set; }
        IFileStorageSettings Attachment { get; set; }
        IConnectionStringSettings Storage { get; set; }
    }
}
