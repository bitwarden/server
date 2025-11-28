// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.AdminConsole.Models.Response.Providers;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Api.Models.Response;

public class ProfileResponseModel : ResponseModel
{
    public ProfileResponseModel(User user,
        UserAccountKeysData userAccountKeysData,
        IEnumerable<OrganizationUserOrganizationDetails> organizationsUserDetails,
        IEnumerable<ProviderUserProviderDetails> providerUserDetails,
        IEnumerable<ProviderUserOrganizationDetails> providerUserOrganizationDetails,
        bool twoFactorEnabled,
        bool premiumFromOrganization,
        IEnumerable<Guid> organizationIdsClaimingUser) : base("profile")
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        EmailVerified = user.EmailVerified;
        Premium = user.Premium;
        PremiumFromOrganization = premiumFromOrganization;
        Culture = user.Culture;
        TwoFactorEnabled = twoFactorEnabled;
        Key = user.Key;
        PrivateKey = user.PrivateKey;
        AccountKeys = userAccountKeysData != null ? new PrivateKeysResponseModel(userAccountKeysData) : null;
        SecurityStamp = user.SecurityStamp;
        ForcePasswordReset = user.ForcePasswordReset;
        UsesKeyConnector = user.UsesKeyConnector;
        AvatarColor = user.AvatarColor;
        CreationDate = user.CreationDate;
        VerifyDevices = user.VerifyDevices;
        Organizations = organizationsUserDetails?.Select(o => new ProfileOrganizationResponseModel(o, organizationIdsClaimingUser));
        Providers = providerUserDetails?.Select(p => new ProfileProviderResponseModel(p));
        ProviderOrganizations =
            providerUserOrganizationDetails?.Select(po => new ProfileProviderOrganizationResponseModel(po));
    }

    public ProfileResponseModel() : base("profile")
    {
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool EmailVerified { get; set; }
    public bool Premium { get; set; }
    public bool PremiumFromOrganization { get; set; }
    public string Culture { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string Key { get; set; }
    [Obsolete("Use AccountKeys instead.")]
    public string PrivateKey { get; set; }
    public PrivateKeysResponseModel AccountKeys { get; set; }
    public string SecurityStamp { get; set; }
    public bool ForcePasswordReset { get; set; }
    public bool UsesKeyConnector { get; set; }
    public string AvatarColor { get; set; }
    public DateTime CreationDate { get; set; }
    public bool VerifyDevices { get; set; }
    public IEnumerable<ProfileOrganizationResponseModel> Organizations { get; set; }
    public IEnumerable<ProfileProviderResponseModel> Providers { get; set; }
    public IEnumerable<ProfileProviderOrganizationResponseModel> ProviderOrganizations { get; set; }
}
