using Bit.Core.Settings;

namespace Bit.Core.Utilities;

public static class GlobalSettingsExtensions
{
    public static bool IsSetupForPushSync(this GlobalSettings globalSettings)
    {
        return globalSettings.SelfHosted &&
            CoreHelpers.SettingHasValue(globalSettings.PushRelayBaseUri) &&
            CoreHelpers.SettingHasValue(globalSettings.Installation.Key) &&
            CoreHelpers.SettingHasValue(globalSettings.Installation.IdentityUri);
    }
}
