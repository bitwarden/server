using System.Security.Claims;
using System.Text.Json;
using AutoFixture;
using Bit.Api.Vault.Controllers;
using Bit.Api.Vault.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(SyncController))]
[SutProviderCustomize]
public class SyncControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Get_ThrowBadRequest_WhenUserNotFound(SutProvider<SyncController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        async Task<SyncResponseModel> GetAction()
        {
            return await sutProvider.Sut.Get();
        }

        await Assert.ThrowsAsync<BadRequestException>((Func<Task<SyncResponseModel>>)GetAction);
    }

    [Theory]
    [BitAutoData]
    public async Task Get_Success_AtLeastOneEnabledOrg(User user,
        List<List<string>> userEquivalentDomains,
        List<GlobalEquivalentDomainsType> userExcludedGlobalEquivalentDomains,
        ICollection<OrganizationUserOrganizationDetails> organizationUserDetails,
        ICollection<ProviderUserProviderDetails> providerUserDetails,
        IEnumerable<ProviderUserOrganizationDetails> providerUserOrganizationDetails,
        ICollection<Folder> folders,
        ICollection<CipherDetails> ciphers,
        ICollection<Send> sends,
        ICollection<Policy> policies,
        ICollection<CollectionDetails> collections,
        SutProvider<SyncController> sutProvider)
    {
        // Get dependencies
        var userService = sutProvider.GetDependency<IUserService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        var folderRepository = sutProvider.GetDependency<IFolderRepository>();
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        var sendRepository = sutProvider.GetDependency<ISendRepository>();
        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();
        var collectionCipherRepository = sutProvider.GetDependency<ICollectionCipherRepository>();
        var userAccountKeysQuery = sutProvider.GetDependency<IUserAccountKeysQuery>();

        // Adjust random data to match required formats / test intentions
        user.EquivalentDomains = JsonSerializer.Serialize(userEquivalentDomains);
        user.ExcludedGlobalEquivalentDomains = JsonSerializer.Serialize(userExcludedGlobalEquivalentDomains);

        // At least 1 org needs to be enabled to fully test
        if (!organizationUserDetails.Any(o => o.Enabled))
        {
            // We need at least 1 enabled org
            if (organizationUserDetails.Count > 0)
            {
                organizationUserDetails.First().Enabled = true;
            }
            else
            {
                // create an enabled org
                var enabledOrg = new Fixture().Create<OrganizationUserOrganizationDetails>();
                enabledOrg.Enabled = true;
                organizationUserDetails.Add((enabledOrg));
            }
        }

        // Setup returns
        userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(user);
        userAccountKeysQuery.Run(user).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = user.GetPublicKeyEncryptionKeyPair(),
            SignatureKeyPairData = null,
        });

        organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed).Returns(organizationUserDetails);

        providerUserRepository
            .GetManyDetailsByUserAsync(user.Id, ProviderUserStatusType.Confirmed).Returns(providerUserDetails);

        providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(user.Id, ProviderUserStatusType.Confirmed)
            .Returns(providerUserOrganizationDetails);

        folderRepository.GetManyByUserIdAsync(user.Id).Returns(folders);
        cipherRepository.GetManyByUserIdAsync(user.Id).Returns(ciphers);

        sendRepository
            .GetManyByUserIdAsync(user.Id).Returns(sends);

        policyRepository.GetManyByUserIdAsync(user.Id).Returns(policies);

        // Returns for methods only called if we have enabled orgs
        collectionRepository.GetManyByUserIdAsync(user.Id).Returns(collections);
        collectionCipherRepository.GetManyByUserIdAsync(user.Id).Returns(new List<CollectionCipher>());
        // Back to standard test setup
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user).Returns(false);
        userService.HasPremiumFromOrganization(user).Returns(false);

        // Execute GET
        var result = await sutProvider.Sut.Get();

        // Asserts
        // Assert that methods are called
        var hasEnabledOrgs = organizationUserDetails.Any(o => o.Enabled);
        await this.AssertMethodsCalledAsync(userService, twoFactorIsEnabledQuery, organizationUserRepository, providerUserRepository, folderRepository,
            cipherRepository, sendRepository, collectionRepository, collectionCipherRepository, hasEnabledOrgs);

        Assert.IsType<SyncResponseModel>(result);

        // Collections should not be empty when at least 1 org is enabled
        Assert.NotEmpty(result.Collections);
    }


    [Theory]
    [BitAutoData]
    public async Task Get_Success_AllDisabledOrgs(User user,
        List<List<string>> userEquivalentDomains,
        List<GlobalEquivalentDomainsType> userExcludedGlobalEquivalentDomains,
        ICollection<OrganizationUserOrganizationDetails> organizationUserDetails,
        ICollection<ProviderUserProviderDetails> providerUserDetails,
        IEnumerable<ProviderUserOrganizationDetails> providerUserOrganizationDetails,
        ICollection<Folder> folders,
        ICollection<CipherDetails> ciphers,
        ICollection<Send> sends,
        ICollection<Policy> policies,
        SutProvider<SyncController> sutProvider)
    {
        // Get dependencies
        var userService = sutProvider.GetDependency<IUserService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        var folderRepository = sutProvider.GetDependency<IFolderRepository>();
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        var sendRepository = sutProvider.GetDependency<ISendRepository>();
        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();
        var collectionCipherRepository = sutProvider.GetDependency<ICollectionCipherRepository>();
        var userAccountKeysQuery = sutProvider.GetDependency<IUserAccountKeysQuery>();

        // Adjust random data to match required formats / test intentions
        user.EquivalentDomains = JsonSerializer.Serialize(userEquivalentDomains);
        user.ExcludedGlobalEquivalentDomains = JsonSerializer.Serialize(userExcludedGlobalEquivalentDomains);

        // All orgs disabled
        if (organizationUserDetails.Count > 0)
        {
            foreach (var orgUserDetails in organizationUserDetails)
            {
                orgUserDetails.Enabled = false;
            }
        }
        else
        {
            var disabledOrg = new Fixture().Create<OrganizationUserOrganizationDetails>();
            disabledOrg.Enabled = false;
            organizationUserDetails.Add((disabledOrg));
        }


        // Setup returns
        userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(user);
        userAccountKeysQuery.Run(user).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = user.GetPublicKeyEncryptionKeyPair(),
            SignatureKeyPairData = null,
        });

        organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed).Returns(organizationUserDetails);

        providerUserRepository
            .GetManyDetailsByUserAsync(user.Id, ProviderUserStatusType.Confirmed).Returns(providerUserDetails);

        providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(user.Id, ProviderUserStatusType.Confirmed)
            .Returns(providerUserOrganizationDetails);

        folderRepository.GetManyByUserIdAsync(user.Id).Returns(folders);
        cipherRepository.GetManyByUserIdAsync(user.Id).Returns(ciphers);

        sendRepository
            .GetManyByUserIdAsync(user.Id).Returns(sends);

        policyRepository.GetManyByUserIdAsync(user.Id).Returns(policies);

        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user).Returns(false);
        userService.HasPremiumFromOrganization(user).Returns(false);

        // Execute GET
        var result = await sutProvider.Sut.Get();


        // Asserts
        // Assert that methods are called

        var hasEnabledOrgs = organizationUserDetails.Any(o => o.Enabled);
        await this.AssertMethodsCalledAsync(userService, twoFactorIsEnabledQuery, organizationUserRepository, providerUserRepository, folderRepository,
            cipherRepository, sendRepository, collectionRepository, collectionCipherRepository, hasEnabledOrgs);

        Assert.IsType<SyncResponseModel>(result);

        // Collections should be empty when all standard orgs are disabled.
        Assert.Empty(result.Collections);
    }


    // Test where provider org has specific plan type and assert plan type comes out on SyncResponseModel class on ProfileResponseModel
    [Theory]
    [BitAutoData]
    public async Task Get_ProviderPlanTypeProperlyPopulated(User user,
        List<List<string>> userEquivalentDomains,
        List<GlobalEquivalentDomainsType> userExcludedGlobalEquivalentDomains,
        ICollection<OrganizationUserOrganizationDetails> organizationUserDetails,
        ICollection<ProviderUserProviderDetails> providerUserDetails,
        IEnumerable<ProviderUserOrganizationDetails> providerUserOrganizationDetails,
        ICollection<Folder> folders,
        ICollection<CipherDetails> ciphers,
        ICollection<Send> sends,
        ICollection<Policy> policies,
        ICollection<CollectionDetails> collections,
        SutProvider<SyncController> sutProvider)
    {
        // Get dependencies
        var userService = sutProvider.GetDependency<IUserService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        var folderRepository = sutProvider.GetDependency<IFolderRepository>();
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        var sendRepository = sutProvider.GetDependency<ISendRepository>();
        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();
        var collectionCipherRepository = sutProvider.GetDependency<ICollectionCipherRepository>();
        var userAccountKeysQuery = sutProvider.GetDependency<IUserAccountKeysQuery>();

        // Adjust random data to match required formats / test intentions
        user.EquivalentDomains = JsonSerializer.Serialize(userEquivalentDomains);
        user.ExcludedGlobalEquivalentDomains = JsonSerializer.Serialize(userExcludedGlobalEquivalentDomains);


        // Setup returns
        userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(user);

        organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed).Returns(organizationUserDetails);

        providerUserRepository
            .GetManyDetailsByUserAsync(user.Id, ProviderUserStatusType.Confirmed).Returns(providerUserDetails);

        foreach (var p in providerUserOrganizationDetails)
        {
            p.SsoConfig = null;
        }
        providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(user.Id, ProviderUserStatusType.Confirmed)
            .Returns(providerUserOrganizationDetails);

        folderRepository.GetManyByUserIdAsync(user.Id).Returns(folders);
        cipherRepository.GetManyByUserIdAsync(user.Id).Returns(ciphers);

        sendRepository
            .GetManyByUserIdAsync(user.Id).Returns(sends);

        policyRepository.GetManyByUserIdAsync(user.Id).Returns(policies);

        // Returns for methods only called if we have enabled orgs
        collectionRepository.GetManyByUserIdAsync(user.Id).Returns(collections);
        collectionCipherRepository.GetManyByUserIdAsync(user.Id).Returns(new List<CollectionCipher>());
        // Back to standard test setup
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user).Returns(false);
        userService.HasPremiumFromOrganization(user).Returns(false);

        userAccountKeysQuery.Run(user).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = user.GetPublicKeyEncryptionKeyPair(),
            SignatureKeyPairData = null,
        });

        // Execute GET
        var result = await sutProvider.Sut.Get();

        // Asserts
        // Assert that methods are called

        var hasEnabledOrgs = organizationUserDetails.Any(o => o.Enabled);
        await this.AssertMethodsCalledAsync(userService, twoFactorIsEnabledQuery, organizationUserRepository, providerUserRepository, folderRepository,
            cipherRepository, sendRepository, collectionRepository, collectionCipherRepository, hasEnabledOrgs);

        Assert.IsType<SyncResponseModel>(result);

        // Look up ProviderOrg output and compare to ProviderOrg method inputs to ensure
        // product type is set correctly.
        foreach (var profProviderOrg in result.Profile.ProviderOrganizations)
        {
            var matchedProviderUserOrgDetails =
                providerUserOrganizationDetails.FirstOrDefault(p => p.OrganizationId == profProviderOrg.Id);

            if (matchedProviderUserOrgDetails != null)
            {
                var providerOrgProductType = MockPlans.Get(matchedProviderUserOrgDetails.PlanType).ProductTier;
                Assert.Equal(providerOrgProductType, profProviderOrg.ProductTierType);
            }
        }
    }

    [Theory]
    [BitAutoData]
    public async Task Get_HaveNoMasterPassword_UserDecryptionMasterPasswordUnlockIsNull(
        User user, SutProvider<SyncController> sutProvider)
    {
        user.EquivalentDomains = null;
        user.ExcludedGlobalEquivalentDomains = null;

        user.MasterPassword = null;

        var userAccountKeysQuery = sutProvider.GetDependency<IUserAccountKeysQuery>();
        userAccountKeysQuery.Run(user).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = user.GetPublicKeyEncryptionKeyPair(),
            SignatureKeyPairData = null,
        });

        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(user);

        var result = await sutProvider.Sut.Get();

        Assert.Null(result.UserDecryption.MasterPasswordUnlock);
    }

    [Theory]
    [BitAutoData(KdfType.PBKDF2_SHA256, 654_321, null, null)]
    [BitAutoData(KdfType.Argon2id, 11, 128, 5)]
    public async Task Get_HaveMasterPassword_UserDecryptionMasterPasswordUnlockNotNull(
        KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism,
        User user, SutProvider<SyncController> sutProvider)
    {
        user.EquivalentDomains = null;
        user.ExcludedGlobalEquivalentDomains = null;

        user.Key = "test-key";
        user.MasterPassword = "test-master-password";
        user.Kdf = kdfType;
        user.KdfIterations = kdfIterations;
        user.KdfMemory = kdfMemory;
        user.KdfParallelism = kdfParallelism;

        var userAccountKeysQuery = sutProvider.GetDependency<IUserAccountKeysQuery>();
        userAccountKeysQuery.Run(user).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = user.GetPublicKeyEncryptionKeyPair(),
            SignatureKeyPairData = null,
        });

        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(user);

        var result = await sutProvider.Sut.Get();

        Assert.NotNull(result.UserDecryption.MasterPasswordUnlock);
        Assert.NotNull(result.UserDecryption.MasterPasswordUnlock.Kdf);
        Assert.Equal(kdfType, result.UserDecryption.MasterPasswordUnlock.Kdf.KdfType);
        Assert.Equal(kdfIterations, result.UserDecryption.MasterPasswordUnlock.Kdf.Iterations);
        Assert.Equal(kdfMemory, result.UserDecryption.MasterPasswordUnlock.Kdf.Memory);
        Assert.Equal(kdfParallelism, result.UserDecryption.MasterPasswordUnlock.Kdf.Parallelism);
        Assert.Equal(user.Key, result.UserDecryption.MasterPasswordUnlock.MasterKeyEncryptedUserKey);
        Assert.Equal(user.Email.ToLower(), result.UserDecryption.MasterPasswordUnlock.Salt);
    }

    private async Task AssertMethodsCalledAsync(IUserService userService,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IOrganizationUserRepository organizationUserRepository,
        IProviderUserRepository providerUserRepository, IFolderRepository folderRepository,
        ICipherRepository cipherRepository, ISendRepository sendRepository,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        bool hasEnabledOrgs)
    {
        await userService.ReceivedWithAnyArgs(1).GetUserByPrincipalAsync(default);
        await organizationUserRepository.ReceivedWithAnyArgs(1)
            .GetManyDetailsByUserAsync(default);
        await providerUserRepository.ReceivedWithAnyArgs(1)
            .GetManyDetailsByUserAsync(default);
        await providerUserRepository.ReceivedWithAnyArgs(1)
            .GetManyOrganizationDetailsByUserAsync(default);

        await folderRepository.ReceivedWithAnyArgs(1)
            .GetManyByUserIdAsync(default);

        await cipherRepository.ReceivedWithAnyArgs(1)
            .GetManyByUserIdAsync(default);

        await sendRepository.ReceivedWithAnyArgs(1)
            .GetManyByUserIdAsync(default);

        // These two are only called when at least 1 enabled org.
        if (hasEnabledOrgs)
        {
            await collectionRepository.ReceivedWithAnyArgs(1)
                .GetManyByUserIdAsync(default);
            await collectionCipherRepository.ReceivedWithAnyArgs(1)
                .GetManyByUserIdAsync(default);
        }
        else
        {
            // all disabled orgs
            await collectionRepository.ReceivedWithAnyArgs(0)
                .GetManyByUserIdAsync(default);
            await collectionCipherRepository.ReceivedWithAnyArgs(0)
                .GetManyByUserIdAsync(default);
        }

        await twoFactorIsEnabledQuery.ReceivedWithAnyArgs(1)
            .TwoFactorIsEnabledAsync(default(ITwoFactorProvidersUser));
        await userService.ReceivedWithAnyArgs(1)
            .HasPremiumFromOrganization(default);
    }
}
