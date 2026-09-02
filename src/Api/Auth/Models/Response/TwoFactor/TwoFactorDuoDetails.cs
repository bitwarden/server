using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Duo provider details for both user and organization scopes. Hydrated from
/// <see cref="User"/> or <see cref="Organization"/>; embedded by the per-action
/// <c>TwoFactorDuo*ResponseModel</c> / <c>TwoFactorOrganizationDuo*ResponseModel</c> wrappers.
/// </summary>
public class TwoFactorDuoDetails
{
    public TwoFactorDuoDetails(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        Build(user.GetTwoFactorProvider(TwoFactorProviderType.Duo));
    }

    public TwoFactorDuoDetails(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);
        Build(organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo));
    }

    public bool Enabled { get; set; }
    public string? Host { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    private void Build(TwoFactorProvider? provider)
    {
        if (provider?.MetaData != null && provider.MetaData.Count > 0)
        {
            Enabled = provider.Enabled;

            if (provider.MetaData.TryGetValue("Host", out var host))
            {
                Host = (string)host;
            }
            if (provider.MetaData.TryGetValue("ClientSecret", out var clientSecret))
            {
                ClientSecret = MaskSecret((string)clientSecret);
            }
            if (provider.MetaData.TryGetValue("ClientId", out var clientId))
            {
                ClientId = (string)clientId;
            }
        }
        else
        {
            Enabled = false;
        }
    }

    private static string MaskSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length <= 6)
        {
            return key;
        }

        // Mask all but the first 6 characters.
        return string.Concat(key.AsSpan(0, 6), new string('*', key.Length - 6));
    }
}
