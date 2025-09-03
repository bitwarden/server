#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;

public class EnvironmentRequest
{
    public bool IsSelfHosted { get; init; }
    public PasswordManagerSubscriptionUpdate PasswordManagerSubscriptionUpdate { get; init; }

    public EnvironmentRequest(IGlobalSettings globalSettings, PasswordManagerSubscriptionUpdate passwordManagerSubscriptionUpdate)
    {
        IsSelfHosted = globalSettings.SelfHosted;
        PasswordManagerSubscriptionUpdate = passwordManagerSubscriptionUpdate;
    }
}
