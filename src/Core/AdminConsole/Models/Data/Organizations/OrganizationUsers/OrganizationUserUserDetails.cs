using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserUserDetails : IExternal, ITwoFactorProvidersUser, IOrganizationUser
{
    private Dictionary<TwoFactorProviderType, TwoFactorProvider> _twoFactorProviders;

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string AvatarColor { get; set; }
    public string TwoFactorProviders { get; set; }
    public bool? Premium { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public OrganizationUserType Type { get; set; }
    public bool AccessSecretsManager { get; set; }
    public string ExternalId { get; set; }
    public string SsoExternalId { get; set; }
    public string Permissions { get; set; }
    public string ResetPasswordKey { get; set; }
    public bool UsesKeyConnector { get; set; }
    public bool HasMasterPassword { get; set; }

    public ICollection<Guid> Groups { get; set; } = new List<Guid>();
    public ICollection<CollectionAccessSelection> Collections { get; set; } =
        new List<CollectionAccessSelection>();

    public Dictionary<TwoFactorProviderType, TwoFactorProvider> GetTwoFactorProviders()
    {
        if (string.IsNullOrWhiteSpace(TwoFactorProviders))
        {
            return null;
        }

        try
        {
            if (_twoFactorProviders == null)
            {
                _twoFactorProviders = JsonHelpers.LegacyDeserialize<
                    Dictionary<TwoFactorProviderType, TwoFactorProvider>
                >(TwoFactorProviders);
            }

            return _twoFactorProviders;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    public Guid? GetUserId()
    {
        return UserId;
    }

    public bool GetPremium()
    {
        return Premium.GetValueOrDefault(false);
    }

    public Permissions GetPermissions()
    {
        return string.IsNullOrWhiteSpace(Permissions)
            ? null
            : CoreHelpers.LoadClassFromJsonData<Permissions>(Permissions);
    }
}
