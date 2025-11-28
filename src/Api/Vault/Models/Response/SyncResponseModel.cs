// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Response;
using Bit.Api.Tools.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models.Response;

public class SyncResponseModel() : ResponseModel("sync")
{
    public SyncResponseModel(
        GlobalSettings globalSettings,
        User user,
        UserAccountKeysData userAccountKeysData,
        bool userTwoFactorEnabled,
        bool userHasPremiumFromOrganization,
        IDictionary<Guid, OrganizationAbility> organizationAbilities,
        IEnumerable<Guid> organizationIdsClaimingingUser,
        IEnumerable<OrganizationUserOrganizationDetails> organizationUserDetails,
        IEnumerable<ProviderUserProviderDetails> providerUserDetails,
        IEnumerable<ProviderUserOrganizationDetails> providerUserOrganizationDetails,
        IEnumerable<Folder> folders,
        IEnumerable<CollectionDetails> collections,
        IEnumerable<CipherDetails> ciphers,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersDict,
        bool excludeDomains,
        IEnumerable<Policy> policies,
        IEnumerable<Send> sends)
        : this()
    {
        Profile = new ProfileResponseModel(user, userAccountKeysData, organizationUserDetails, providerUserDetails,
            providerUserOrganizationDetails, userTwoFactorEnabled, userHasPremiumFromOrganization, organizationIdsClaimingingUser);
        Folders = folders.Select(f => new FolderResponseModel(f));
        Ciphers = ciphers.Select(cipher =>
            new CipherDetailsResponseModel(
                cipher,
                user,
                organizationAbilities,
                globalSettings,
                collectionCiphersDict));
        Collections = collections?.Select(
            c => new CollectionDetailsResponseModel(c)) ?? new List<CollectionDetailsResponseModel>();
        Domains = excludeDomains ? null : new DomainsResponseModel(user, false);
        Policies = policies?.Select(p => new PolicyResponseModel(p)) ?? new List<PolicyResponseModel>();
        Sends = sends.Select(s => new SendResponseModel(s, globalSettings));
        UserDecryption = new UserDecryptionResponseModel
        {
            MasterPasswordUnlock = user.HasMasterPassword()
                ? new MasterPasswordUnlockResponseModel
                {
                    Kdf = new MasterPasswordUnlockKdfResponseModel
                    {
                        KdfType = user.Kdf,
                        Iterations = user.KdfIterations,
                        Memory = user.KdfMemory,
                        Parallelism = user.KdfParallelism
                    },
                    MasterKeyEncryptedUserKey = user.Key!,
                    Salt = user.Email.ToLowerInvariant()
                }
                : null
        };
    }

    public ProfileResponseModel Profile { get; set; }
    public IEnumerable<FolderResponseModel> Folders { get; set; }
    public IEnumerable<CollectionDetailsResponseModel> Collections { get; set; }
    public IEnumerable<CipherDetailsResponseModel> Ciphers { get; set; }
    public DomainsResponseModel Domains { get; set; }
    public IEnumerable<PolicyResponseModel> Policies { get; set; }
    public IEnumerable<SendResponseModel> Sends { get; set; }
    public UserDecryptionResponseModel UserDecryption { get; set; }
}
