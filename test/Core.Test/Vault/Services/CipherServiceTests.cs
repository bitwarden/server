using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[UserCipherCustomize]
[SutProviderCustomize]
public class CipherServiceTests
{
    [Theory, BitAutoData]
    public async Task SaveAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider, Cipher cipher)
    {
        var lastKnownRevisionDate = cipher.RevisionDate.AddDays(-1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(cipher, cipher.UserId.Value, lastKnownRevisionDate));
        Assert.Contains("out of date", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider,
        CipherDetails cipherDetails)
    {
        var lastKnownRevisionDate = cipherDetails.RevisionDate.AddDays(-1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveDetailsAsync(cipherDetails, cipherDetails.UserId.Value, lastKnownRevisionDate));
        Assert.Contains("out of date", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ShareAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider, Cipher cipher,
        Organization organization, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var lastKnownRevisionDate = cipher.RevisionDate.AddDays(-1);
        cipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [Guid.NewGuid().ToString()] = new CipherAttachment.MetaData { }
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
            lastKnownRevisionDate));
        Assert.Contains("out of date", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ShareManyAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider,
        IEnumerable<CipherDetails> ciphers, Guid organizationId, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(new Organization
            {
                PlanType = PlanType.EnterpriseAnnually,
                MaxStorageGb = 100
            });

        var cipherInfos = ciphers.Select(c => (c, (DateTime?)c.RevisionDate.AddDays(-1)));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ShareManyAsync(cipherInfos, organizationId, collectionIds, ciphers.First().UserId.Value));
        Assert.Contains("out of date", exception.Message);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("Correct Time")]
    public async Task SaveAsync_CorrectRevisionDate_Passes(string revisionDateString,
        SutProvider<CipherService> sutProvider, Cipher cipher)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;

        await sutProvider.Sut.SaveAsync(cipher, cipher.UserId.Value, lastKnownRevisionDate);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).ReplaceAsync(cipher);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("Correct Time")]
    public async Task SaveDetailsAsync_CorrectRevisionDate_Passes(string revisionDateString,
        SutProvider<CipherService> sutProvider, CipherDetails cipherDetails)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipherDetails.RevisionDate;

        await sutProvider.Sut.SaveDetailsAsync(cipherDetails, cipherDetails.UserId.Value, lastKnownRevisionDate);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).ReplaceAsync(cipherDetails);
    }

    [Theory, BitAutoData]
    public async Task CreateAttachmentAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider, Cipher cipher, Guid savingUserId)
    {
        var lastKnownRevisionDate = cipher.RevisionDate.AddDays(-1);
        var stream = new MemoryStream();
        var fileName = "test.txt";
        var key = "test-key";

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateAttachmentAsync(cipher, stream, fileName, key, 100, savingUserId, false, lastKnownRevisionDate));
        Assert.Contains("out of date", exception.Message);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("Correct Time")]
    public async Task CreateAttachmentAsync_CorrectRevisionDate_DoesNotThrow(string revisionDateString,
        SutProvider<CipherService> sutProvider, CipherDetails cipher, Guid savingUserId)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
        var stream = new MemoryStream(new byte[100]);
        var fileName = "test.txt";
        var key = "test-key";

        // Setup cipher with user ownership
        cipher.UserId = savingUserId;
        cipher.OrganizationId = null;

        // Mock user storage and premium access
        var user = new User { Id = savingUserId, MaxStorageGb = 1 };
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(savingUserId)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        sutProvider.GetDependency<IAttachmentStorageService>()
            .UploadNewAttachmentAsync(Arg.Any<Stream>(), cipher, Arg.Any<CipherAttachment.MetaData>())
            .Returns(Task.CompletedTask);

        sutProvider.GetDependency<IAttachmentStorageService>()
            .ValidateFileAsync(cipher, Arg.Any<CipherAttachment.MetaData>(), Arg.Any<long>())
            .Returns((true, 100L));

        sutProvider.GetDependency<ICipherRepository>()
            .UpdateAttachmentAsync(Arg.Any<CipherAttachment>())
            .Returns(Task.CompletedTask);

        sutProvider.GetDependency<ICipherRepository>()
            .ReplaceAsync(Arg.Any<CipherDetails>())
            .Returns(Task.CompletedTask);

        await sutProvider.Sut.CreateAttachmentAsync(cipher, stream, fileName, key, 100, savingUserId, false, lastKnownRevisionDate);

        await sutProvider.GetDependency<IAttachmentStorageService>().Received(1)
            .UploadNewAttachmentAsync(Arg.Any<Stream>(), cipher, Arg.Any<CipherAttachment.MetaData>());
    }

    [Theory, BitAutoData]
    public async Task CreateAttachmentForDelayedUploadAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider, Cipher cipher, Guid savingUserId)
    {
        var lastKnownRevisionDate = cipher.RevisionDate.AddDays(-1);
        var key = "test-key";
        var fileName = "test.txt";
        var fileSize = 100L;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateAttachmentForDelayedUploadAsync(cipher, key, fileName, fileSize, false, savingUserId, lastKnownRevisionDate));
        Assert.Contains("out of date", exception.Message);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("Correct Time")]
    public async Task CreateAttachmentForDelayedUploadAsync_CorrectRevisionDate_DoesNotThrow(string revisionDateString,
        SutProvider<CipherService> sutProvider, CipherDetails cipher, Guid savingUserId)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
        var key = "test-key";
        var fileName = "test.txt";
        var fileSize = 100L;

        // Setup cipher with user ownership
        cipher.UserId = savingUserId;
        cipher.OrganizationId = null;

        // Mock user storage and premium access
        var user = new User { Id = savingUserId, MaxStorageGb = 1 };
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(savingUserId)
            .Returns(user);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(user)
            .Returns(true);

        sutProvider.GetDependency<IAttachmentStorageService>()
            .GetAttachmentUploadUrlAsync(cipher, Arg.Any<CipherAttachment.MetaData>())
            .Returns("https://example.com/upload");

        sutProvider.GetDependency<ICipherRepository>()
            .UpdateAttachmentAsync(Arg.Any<CipherAttachment>())
            .Returns(Task.CompletedTask);

        var result = await sutProvider.Sut.CreateAttachmentForDelayedUploadAsync(cipher, key, fileName, fileSize, false, savingUserId, lastKnownRevisionDate);

        Assert.NotNull(result.attachmentId);
        Assert.NotNull(result.uploadUrl);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveDetailsAsync_PersonalVault_WithOrganizationDataOwnershipPolicyEnabled_Throws(
        SutProvider<CipherService> sutProvider,
        CipherDetails cipher,
        Guid savingUserId)
    {
        cipher.Id = default;
        cipher.UserId = savingUserId;
        cipher.OrganizationId = null;

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(savingUserId, PolicyType.OrganizationDataOwnership)
            .Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveDetailsAsync(cipher, savingUserId, null));
        Assert.Contains("restricted from saving items to your personal vault", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveDetailsAsync_PersonalVault_WithOrganizationDataOwnershipPolicyDisabled_Succeeds(
        SutProvider<CipherService> sutProvider,
        CipherDetails cipher,
        Guid savingUserId)
    {
        cipher.Id = default;
        cipher.UserId = savingUserId;
        cipher.OrganizationId = null;

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(savingUserId, PolicyType.OrganizationDataOwnership)
            .Returns(false);

        await sutProvider.Sut.SaveDetailsAsync(cipher, savingUserId, null);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .CreateAsync(cipher);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveDetailsAsync_PersonalVault_WithPolicyRequirementsEnabled_WithOrganizationDataOwnershipPolicyEnabled_Throws(
        SutProvider<CipherService> sutProvider,
        CipherDetails cipher,
        Guid savingUserId)
    {
        cipher.Id = default;
        cipher.UserId = savingUserId;
        cipher.OrganizationId = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(savingUserId)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(
                OrganizationDataOwnershipState.Enabled,
                [new PolicyDetails()]));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveDetailsAsync(cipher, savingUserId, null));
        Assert.Contains("restricted from saving items to your personal vault", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveDetailsAsync_PersonalVault_WithPolicyRequirementsEnabled_WithOrganizationDataOwnershipPolicyDisabled_Succeeds(
        SutProvider<CipherService> sutProvider,
        CipherDetails cipher,
        Guid savingUserId)
    {
        cipher.Id = default;
        cipher.UserId = savingUserId;
        cipher.OrganizationId = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(savingUserId)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(
                OrganizationDataOwnershipState.Disabled,
                []));

        await sutProvider.Sut.SaveDetailsAsync(cipher, savingUserId, null);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .CreateAsync(cipher);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("Correct Time")]
    public async Task ShareAsync_CorrectRevisionDate_Passes(string revisionDateString,
        SutProvider<CipherService> sutProvider, Cipher cipher, Organization organization, List<Guid> collectionIds)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        cipherRepository.ReplaceAsync(cipher, collectionIds).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        cipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [Guid.NewGuid().ToString()] = new CipherAttachment.MetaData { }
        });
        await sutProvider.Sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
            lastKnownRevisionDate);
        await cipherRepository.Received(1).ReplaceAsync(cipher, collectionIds);
    }

    [Theory]
    [BitAutoData("Correct Time")]
    public async Task ShareAsync_FailReplace_Throws(string revisionDateString,
        SutProvider<CipherService> sutProvider, Cipher cipher, Organization organization, List<Guid> collectionIds)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        cipherRepository.ReplaceAsync(cipher, collectionIds).Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        cipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [Guid.NewGuid().ToString()] = new CipherAttachment.MetaData { }
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
            lastKnownRevisionDate));
        Assert.Contains("Unable to save", exception.Message);
    }

    [Theory]
    [BitAutoData("Correct Time")]
    public async Task ShareAsync_HasV0Attachments_ReplaceAttachmentMetadataWithNewOneBeforeSavingCipher(string revisionDateString,
        SutProvider<CipherService> sutProvider, Cipher cipher, Organization organization, List<Guid> collectionIds)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
        var originalCipher = CoreHelpers.CloneObject(cipher);
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        cipherRepository.ReplaceAsync(cipher, collectionIds).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var pushNotificationService = sutProvider.GetDependency<IPushNotificationService>();

        var v0AttachmentId = Guid.NewGuid().ToString();
        var anotherAttachmentId = Guid.NewGuid().ToString();
        cipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [v0AttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted"
            },
            [anotherAttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = anotherAttachmentId,
                Key = "AwesomeKey",
                FileName = "AnotherFilename",
                ContainerName = "attachments",
                Size = 300,
                Validated = true
            }
        });

        originalCipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [v0AttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey",
                    Key = "NewAttachmentKey"
                }
            },
            [anotherAttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = anotherAttachmentId,
                Key = "AwesomeKey",
                FileName = "AnotherFilename",
                ContainerName = "attachments",
                Size = 300,
                Validated = true
            }
        });

        await sutProvider.Sut.ShareAsync(originalCipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
            lastKnownRevisionDate);

        await cipherRepository.Received().ReplaceAsync(Arg.Is<Cipher>(c =>
                c.GetAttachments()[v0AttachmentId].Key == "NewAttachmentKey"
                &&
                c.GetAttachments()[v0AttachmentId].FileName == "AFileNameRe-EncryptedWithOrgKey")
            , collectionIds);

        await pushNotificationService.Received(1).PushSyncCipherUpdateAsync(cipher, collectionIds);
    }

    [Theory]
    [BitAutoData("Correct Time")]
    public async Task ShareAsync_HasV0Attachments_StartSharingThoseAttachments(string revisionDateString,
        SutProvider<CipherService> sutProvider, Cipher cipher, Organization organization, List<Guid> collectionIds)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
        var originalCipher = CoreHelpers.CloneObject(cipher);
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        cipherRepository.ReplaceAsync(cipher, collectionIds).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var attachmentStorageService = sutProvider.GetDependency<IAttachmentStorageService>();

        var v0AttachmentId = Guid.NewGuid().ToString();
        var anotherAttachmentId = Guid.NewGuid().ToString();
        cipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [v0AttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey",
                    Key = "NewAttachmentKey"
                }
            },
            [anotherAttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = anotherAttachmentId,
                Key = "AwesomeKey",
                FileName = "AnotherFilename",
                ContainerName = "attachments",
                Size = 300,
                Validated = true
            }
        });

        originalCipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [v0AttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey",
                    Key = "NewAttachmentKey"
                }
            },
            [anotherAttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = anotherAttachmentId,
                Key = "AwesomeKey",
                FileName = "AnotherFilename",
                ContainerName = "attachments",
                Size = 300,
                Validated = true
            }
        });

        await sutProvider.Sut.ShareAsync(originalCipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
            lastKnownRevisionDate);

        await attachmentStorageService.Received().StartShareAttachmentAsync(cipher.Id,
            organization.Id,
            Arg.Is<CipherAttachment.MetaData>(m => m.Key == "NewAttachmentKey" && m.FileName == "AFileNameRe-EncryptedWithOrgKey"));

        await attachmentStorageService.Received(0).StartShareAttachmentAsync(cipher.Id,
            organization.Id,
            Arg.Is<CipherAttachment.MetaData>(m => m.Key == "AwesomeKey" && m.FileName == "AnotherFilename"));

        await attachmentStorageService.Received().CleanupAsync(cipher.Id);
    }

    [Theory]
    [BitAutoData("Correct Time")]
    public async Task ShareAsync_HasV0Attachments_StartShareThrows_PerformsRollback_Rethrows(string revisionDateString,
        SutProvider<CipherService> sutProvider, Cipher cipher, Organization organization, List<Guid> collectionIds)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
        var originalCipher = CoreHelpers.CloneObject(cipher);
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        cipherRepository.ReplaceAsync(cipher, collectionIds).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var attachmentStorageService = sutProvider.GetDependency<IAttachmentStorageService>();
        var collectionCipherRepository = sutProvider.GetDependency<ICollectionCipherRepository>();
        collectionCipherRepository.GetManyByUserIdCipherIdAsync(cipher.UserId.Value, cipher.Id).Returns(
            Task.FromResult((ICollection<CollectionCipher>)new List<CollectionCipher>
            {
                new CollectionCipher
                {
                    CipherId = cipher.Id,
                    CollectionId = collectionIds[0]
                },
                new CollectionCipher
                {
                    CipherId = cipher.Id,
                    CollectionId = Guid.NewGuid()
                }
            }));

        var v0AttachmentId = Guid.NewGuid().ToString();
        var anotherAttachmentId = Guid.NewGuid().ToString();
        cipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [v0AttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey",
                    Key = "NewAttachmentKey"
                }
            },
            [anotherAttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = anotherAttachmentId,
                Key = "AwesomeKey",
                FileName = "AnotherFilename",
                ContainerName = "attachments",
                Size = 300,
                Validated = true
            }
        });

        originalCipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [v0AttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey",
                    Key = "NewAttachmentKey"
                }
            },
            [anotherAttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = anotherAttachmentId,
                Key = "AwesomeKey",
                FileName = "AnotherFilename",
                ContainerName = "attachments",
                Size = 300,
                Validated = true
            }
        });

        attachmentStorageService.StartShareAttachmentAsync(cipher.Id,
            organization.Id,
            Arg.Is<CipherAttachment.MetaData>(m => m.AttachmentId == v0AttachmentId))
            .Returns(Task.FromException(new InvalidOperationException("ex from StartShareAttachmentAsync")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
            lastKnownRevisionDate));
        Assert.Contains("ex from StartShareAttachmentAsync", exception.Message);

        await collectionCipherRepository.Received().UpdateCollectionsAsync(cipher.Id, cipher.UserId.Value,
            Arg.Is<List<Guid>>(ids => ids.Count == 1 && ids[0] != collectionIds[0]));

        await cipherRepository.Received().ReplaceAsync(Arg.Is<Cipher>(c =>
                c.GetAttachments()[v0AttachmentId].Key == null
                &&
                c.GetAttachments()[v0AttachmentId].FileName == "AFileNameEncrypted"
                &&
                c.GetAttachments()[v0AttachmentId].TempMetadata == null)
        );
    }

    [Theory]
    [BitAutoData("Correct Time")]
    public async Task ShareAsync_HasSeveralV0Attachments_StartShareThrowsOnSecondOne_PerformsRollback_Rethrows(string revisionDateString,
        SutProvider<CipherService> sutProvider, Cipher cipher, Organization organization, List<Guid> collectionIds)
    {
        var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
        var originalCipher = CoreHelpers.CloneObject(cipher);
        var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
        cipherRepository.ReplaceAsync(cipher, collectionIds).Returns(true);
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        var attachmentStorageService = sutProvider.GetDependency<IAttachmentStorageService>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var collectionCipherRepository = sutProvider.GetDependency<ICollectionCipherRepository>();
        collectionCipherRepository.GetManyByUserIdCipherIdAsync(cipher.UserId.Value, cipher.Id).Returns(
            Task.FromResult((ICollection<CollectionCipher>)new List<CollectionCipher>
            {
                new CollectionCipher
                {
                    CipherId = cipher.Id,
                    CollectionId = collectionIds[0]
                },
                new CollectionCipher
                {
                    CipherId = cipher.Id,
                    CollectionId = Guid.NewGuid()
                }
            }));

        var v0AttachmentId1 = Guid.NewGuid().ToString();
        var v0AttachmentId2 = Guid.NewGuid().ToString();
        var anotherAttachmentId = Guid.NewGuid().ToString();
        cipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [v0AttachmentId1] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId1,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId1,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey",
                    Key = "NewAttachmentKey"
                }
            },
            [v0AttachmentId2] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId2,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted2",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId2,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey2",
                    Key = "NewAttachmentKey2"
                }
            },
            [anotherAttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = anotherAttachmentId,
                Key = "AwesomeKey",
                FileName = "AnotherFilename",
                ContainerName = "attachments",
                Size = 300,
                Validated = true
            }
        });

        originalCipher.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            [v0AttachmentId1] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId1,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId1,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey",
                    Key = "NewAttachmentKey"
                }
            },
            [v0AttachmentId2] = new CipherAttachment.MetaData
            {
                AttachmentId = v0AttachmentId2,
                ContainerName = "attachments",
                FileName = "AFileNameEncrypted2",
                TempMetadata = new CipherAttachment.MetaData
                {
                    AttachmentId = v0AttachmentId2,
                    ContainerName = "attachments",
                    FileName = "AFileNameRe-EncryptedWithOrgKey2",
                    Key = "NewAttachmentKey2"
                }
            },
            [anotherAttachmentId] = new CipherAttachment.MetaData
            {
                AttachmentId = anotherAttachmentId,
                Key = "AwesomeKey",
                FileName = "AnotherFilename",
                ContainerName = "attachments",
                Size = 300,
                Validated = true
            }
        });

        attachmentStorageService.StartShareAttachmentAsync(cipher.Id,
            organization.Id,
            Arg.Is<CipherAttachment.MetaData>(m => m.AttachmentId == v0AttachmentId2))
            .Returns(Task.FromException(new InvalidOperationException("ex from StartShareAttachmentAsync")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
            lastKnownRevisionDate));
        Assert.Contains("ex from StartShareAttachmentAsync", exception.Message);

        await collectionCipherRepository.Received().UpdateCollectionsAsync(cipher.Id, cipher.UserId.Value,
            Arg.Is<List<Guid>>(ids => ids.Count == 1 && ids[0] != collectionIds[0]));

        await cipherRepository.Received().ReplaceAsync(Arg.Is<Cipher>(c =>
                c.GetAttachments()[v0AttachmentId1].Key == null
                &&
                c.GetAttachments()[v0AttachmentId1].FileName == "AFileNameEncrypted"
                &&
                c.GetAttachments()[v0AttachmentId1].TempMetadata == null)
        );

        await cipherRepository.Received().ReplaceAsync(Arg.Is<Cipher>(c =>
                c.GetAttachments()[v0AttachmentId2].Key == null
                &&
                c.GetAttachments()[v0AttachmentId2].FileName == "AFileNameEncrypted2"
                &&
                c.GetAttachments()[v0AttachmentId2].TempMetadata == null)
        );

        await userRepository.UpdateStorageAsync(cipher.UserId.Value);
        await organizationRepository.UpdateStorageAsync(organization.Id);

        await attachmentStorageService.Received().RollbackShareAttachmentAsync(cipher.Id, organization.Id,
            Arg.Is<CipherAttachment.MetaData>(m => m.AttachmentId == v0AttachmentId1), Arg.Any<string>());

        await attachmentStorageService.Received().CleanupAsync(cipher.Id);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("Correct Time")]
    public async Task ShareManyAsync_CorrectRevisionDate_Passes(string revisionDateString,
        SutProvider<CipherService> sutProvider, IEnumerable<CipherDetails> ciphers, Organization organization, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id)
            .Returns(new Organization
            {
                PlanType = PlanType.EnterpriseAnnually,
                MaxStorageGb = 100
            });

        var cipherInfos = ciphers.Select(c => (c,
            string.IsNullOrEmpty(revisionDateString) ? null : (DateTime?)c.RevisionDate));
        var sharingUserId = ciphers.First().UserId.Value;

        await sutProvider.Sut.ShareManyAsync(cipherInfos, organization.Id, collectionIds, sharingUserId);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).UpdateCiphersAsync(sharingUserId,
            Arg.Is<IEnumerable<Cipher>>(arg => !arg.Except(ciphers).Any()));
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreAsync_UpdatesUserCipher(Guid restoringUserId, CipherDetails cipher, SutProvider<CipherService> sutProvider)
    {
        cipher.UserId = restoringUserId;
        cipher.OrganizationId = null;

        var initialRevisionDate = new DateTime(1970, 1, 1, 0, 0, 0);
        cipher.DeletedDate = initialRevisionDate;
        cipher.RevisionDate = initialRevisionDate;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(restoringUserId)
            .Returns(new User
            {
                Id = restoringUserId,
            });

        await sutProvider.Sut.RestoreAsync(cipher, restoringUserId);

        Assert.Null(cipher.DeletedDate);
        Assert.NotEqual(initialRevisionDate, cipher.RevisionDate);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task RestoreAsync_UpdatesOrganizationCipher(Guid restoringUserId, CipherDetails cipher, User user, SutProvider<CipherService> sutProvider)
    {
        cipher.OrganizationId = Guid.NewGuid();
        cipher.Edit = false;
        cipher.Manage = true;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(restoringUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipher.OrganizationId.Value)
            .Returns(new OrganizationAbility
            {
                Id = cipher.OrganizationId.Value,
                LimitItemDeletion = true
            });

        var initialRevisionDate = new DateTime(1970, 1, 1, 0, 0, 0);
        cipher.DeletedDate = initialRevisionDate;
        cipher.RevisionDate = initialRevisionDate;

        await sutProvider.Sut.RestoreAsync(cipher, restoringUserId);

        Assert.Null(cipher.DeletedDate);
        Assert.NotEqual(initialRevisionDate, cipher.RevisionDate);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreAsync_WithAlreadyRestoredCipher_SkipsOperation(
        Guid restoringUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.DeletedDate = null;

        await sutProvider.Sut.RestoreAsync(cipherDetails, restoringUserId, true);

        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().UpsertAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCipherEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushSyncCipherUpdateAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreAsync_WithPersonalCipherBelongingToDifferentUser_ThrowsBadRequestException(
        Guid restoringUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.UserId = Guid.NewGuid();
        cipherDetails.OrganizationId = null;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(restoringUserId)
            .Returns(new User
            {
                Id = restoringUserId,
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreAsync(cipherDetails, restoringUserId));

        Assert.Contains("do not have permissions", exception.Message);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().UpsertAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCipherEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushSyncCipherUpdateAsync(default, default);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task RestoreAsync_WithOrgAdminOverride_RestoresCipher(
        Guid restoringUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.DeletedDate = DateTime.UtcNow;

        await sutProvider.Sut.RestoreAsync(cipherDetails, restoringUserId, true);

        Assert.Null(cipherDetails.DeletedDate);
        Assert.NotEqual(DateTime.UtcNow, cipherDetails.RevisionDate);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).UpsertAsync(cipherDetails);
        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventAsync(cipherDetails, EventType.Cipher_Restored);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCipherUpdateAsync(cipherDetails, null);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task RestoreAsync_WithManagePermission_RestoresCipher(
        Guid restoringUserId, CipherDetails cipherDetails, User user, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();
        cipherDetails.DeletedDate = DateTime.UtcNow;
        cipherDetails.Edit = false;
        cipherDetails.Manage = true;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(restoringUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipherDetails.OrganizationId.Value)
            .Returns(new OrganizationAbility
            {
                Id = cipherDetails.OrganizationId.Value,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.RestoreAsync(cipherDetails, restoringUserId);

        Assert.Null(cipherDetails.DeletedDate);
        Assert.NotEqual(DateTime.UtcNow, cipherDetails.RevisionDate);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).UpsertAsync(cipherDetails);
        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventAsync(cipherDetails, EventType.Cipher_Restored);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCipherUpdateAsync(cipherDetails, null);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task RestoreAsync_WithoutManagePermission_ThrowsBadRequestException(
        Guid restoringUserId, CipherDetails cipherDetails, User user, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();
        cipherDetails.DeletedDate = DateTime.UtcNow;
        cipherDetails.Edit = true;
        cipherDetails.Manage = false;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(restoringUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipherDetails.OrganizationId.Value)
            .Returns(new OrganizationAbility
            {
                Id = cipherDetails.OrganizationId.Value,
                LimitItemDeletion = true
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreAsync(cipherDetails, restoringUserId));

        Assert.Contains("do not have permissions", exception.Message);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().UpsertAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCipherEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushSyncCipherUpdateAsync(default, default);
    }



    [Theory]
    [BitAutoData]
    public async Task RestoreManyAsync_WithOrgAdmin_UpdatesCiphers(Guid organizationId, ICollection<CipherOrganizationDetails> ciphers,
        SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();
        var restoringUserId = ciphers.First().UserId.Value;
        var previousRevisionDate = DateTime.UtcNow;
        foreach (var cipher in ciphers)
        {
            cipher.RevisionDate = previousRevisionDate;
            cipher.OrganizationId = organizationId;
        }

        sutProvider.GetDependency<ICipherRepository>().GetManyOrganizationDetailsByOrganizationIdAsync(organizationId).Returns(ciphers);
        var revisionDate = previousRevisionDate + TimeSpan.FromMinutes(1);
        sutProvider.GetDependency<ICipherRepository>().RestoreByIdsOrganizationIdAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.All(i => cipherIds.Contains(i))), organizationId).Returns(revisionDate);

        await sutProvider.Sut.RestoreManyAsync(cipherIds, restoringUserId, organizationId, true);

        foreach (var cipher in ciphers)
        {
            Assert.Null(cipher.DeletedDate);
            Assert.Equal(revisionDate, cipher.RevisionDate);
        }

        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventsAsync(Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(events => events.All(e => cipherIds.Contains(e.Item1.Id))));
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCiphersAsync(restoringUserId);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreManyAsync_WithEmptyCipherIdsArray_DoesNothing(Guid restoringUserId,
        SutProvider<CipherService> sutProvider)
    {
        var cipherIds = Array.Empty<Guid>();

        await sutProvider.Sut.RestoreManyAsync(cipherIds, restoringUserId);

        await AssertNoActionsAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreManyAsync_WithNullCipherIdsArray_DoesNothing(Guid restoringUserId,
        SutProvider<CipherService> sutProvider)
    {
        await sutProvider.Sut.RestoreManyAsync(null, restoringUserId);

        await AssertNoActionsAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreManyAsync_WithPersonalCipherBelongingToDifferentUser_DoesNotRestoreCiphers(
        Guid restoringUserId, List<CipherDetails> ciphers, SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();
        var differentUserId = Guid.NewGuid();

        foreach (var cipher in ciphers)
        {
            cipher.UserId = differentUserId;
            cipher.OrganizationId = null;
            cipher.DeletedDate = DateTime.UtcNow;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(restoringUserId)
            .Returns(new List<CipherDetails>());

        var result = await sutProvider.Sut.RestoreManyAsync(cipherIds, restoringUserId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .RestoreAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()), restoringUserId);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(restoringUserId);
    }





    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task RestoreManyAsync_WithManagePermission_RestoresCiphers(
        Guid restoringUserId, List<CipherDetails> ciphers, User user, SutProvider<CipherService> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var cipherIds = ciphers.Select(c => c.Id).ToArray();
        var previousRevisionDate = DateTime.UtcNow;

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
            cipher.Edit = false;
            cipher.Manage = true;
            cipher.DeletedDate = DateTime.UtcNow;
            cipher.RevisionDate = previousRevisionDate;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(restoringUserId)
            .Returns(ciphers);
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(restoringUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                {
                    organizationId, new OrganizationAbility
                    {
                        Id = organizationId,
                        LimitItemDeletion = true
                    }
                }
            });

        var revisionDate = previousRevisionDate + TimeSpan.FromMinutes(1);
        sutProvider.GetDependency<ICipherRepository>()
            .RestoreAsync(Arg.Any<IEnumerable<Guid>>(), restoringUserId)
            .Returns(revisionDate);

        var result = await sutProvider.Sut.RestoreManyAsync(cipherIds, restoringUserId);

        Assert.Equal(ciphers.Count, result.Count);
        foreach (var cipher in result)
        {
            Assert.Null(cipher.DeletedDate);
            Assert.Equal(revisionDate, cipher.RevisionDate);
        }

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .RestoreAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == cipherIds.Count() &&
                ids.All(id => cipherIds.Contains(id))), restoringUserId);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(restoringUserId);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task RestoreManyAsync_WithoutManagePermission_DoesNotRestoreCiphers(
        Guid restoringUserId, List<CipherDetails> ciphers, User user, SutProvider<CipherService> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
            cipher.Edit = true;
            cipher.Manage = false;
            cipher.DeletedDate = DateTime.UtcNow;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(restoringUserId)
            .Returns(ciphers);
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(restoringUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                {
                    organizationId, new OrganizationAbility
                    {
                        Id = organizationId,
                        LimitItemDeletion = true
                    }
                }
            });

        var result = await sutProvider.Sut.RestoreManyAsync(cipherIds, restoringUserId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .RestoreAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()), restoringUserId);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(restoringUserId);
    }

    [Theory, BitAutoData]
    public async Task ShareManyAsync_FreeOrgWithAttachment_Throws(SutProvider<CipherService> sutProvider,
        IEnumerable<CipherDetails> ciphers, Guid organizationId, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(new Organization
        {
            PlanType = PlanType.Free
        });
        ciphers.FirstOrDefault().Attachments =
            "{\"attachment1\":{\"Size\":\"250\",\"FileName\":\"superCoolFile\","
            + "\"Key\":\"superCoolFile\",\"ContainerName\":\"testContainer\",\"Validated\":false}}";

        var cipherInfos = ciphers.Select(c => (c,
           (DateTime?)c.RevisionDate));
        var sharingUserId = ciphers.First().UserId.Value;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ShareManyAsync(cipherInfos, organizationId, collectionIds, sharingUserId));
        Assert.Contains("This organization cannot use attachments", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ShareManyAsync_PaidOrgWithAttachment_Passes(SutProvider<CipherService> sutProvider,
        IEnumerable<CipherDetails> ciphers, Guid organizationId, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(new Organization
            {
                PlanType = PlanType.EnterpriseAnnually,
                MaxStorageGb = 100
            });
        ciphers.FirstOrDefault().Attachments =
            "{\"attachment1\":{\"Size\":\"250\",\"FileName\":\"superCoolFile\","
            + "\"Key\":\"superCoolFile\",\"ContainerName\":\"testContainer\",\"Validated\":false}}";

        var cipherInfos = ciphers.Select(c => (c,
           (DateTime?)c.RevisionDate));
        var sharingUserId = ciphers.First().UserId.Value;

        await sutProvider.Sut.ShareManyAsync(cipherInfos, organizationId, collectionIds, sharingUserId);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).UpdateCiphersAsync(sharingUserId,
            Arg.Is<IEnumerable<Cipher>>(arg => !arg.Except(ciphers).Any()));
    }

    private class SaveDetailsAsyncDependencies
    {
        public CipherDetails CipherDetails { get; set; }
        public SutProvider<CipherService> SutProvider { get; set; }
    }

    private static SaveDetailsAsyncDependencies GetSaveDetailsAsyncDependencies(
        SutProvider<CipherService> sutProvider,
        string newPassword,
        bool viewPassword,
        bool editPermission,
        string? key = null,
        string? totp = null,
        CipherLoginFido2CredentialData[]? passkeys = null,
        CipherFieldData[]? fields = null
        )
    {
        var cipherDetails = new CipherDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = CipherType.Login,
            UserId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
            Key = key,
        };

        var newLoginData = new CipherLoginData { Username = "user", Password = newPassword, Totp = totp, Fido2Credentials = passkeys, Fields = fields };
        cipherDetails.Data = JsonSerializer.Serialize(newLoginData);

        var existingCipher = new Cipher
        {
            Id = cipherDetails.Id,
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(
                new CipherLoginData
                {
                    Username = "user",
                    Password = "OriginalPassword",
                    Totp = "OriginalTotp",
                    Fido2Credentials = []
                }
            ),
        };

        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherDetails.Id)
            .Returns(existingCipher);

        sutProvider.GetDependency<ICipherRepository>()
            .ReplaceAsync(Arg.Any<CipherDetails>())
            .Returns(Task.CompletedTask);

        var permissions = new Dictionary<Guid, OrganizationCipherPermission>
        {
            { cipherDetails.Id, new OrganizationCipherPermission { ViewPassword = viewPassword, Edit = editPermission } }
        };

        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>()
            .GetByOrganization(cipherDetails.OrganizationId.Value)
            .Returns(permissions);

        return new SaveDetailsAsyncDependencies
        {
            CipherDetails = cipherDetails,
            SutProvider = sutProvider,
        };
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_PasswordNotChangedWithoutViewPasswordPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: false, editPermission: true);

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Equal("OriginalPassword", updatedLoginData.Password);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_PasswordNotChangedWithoutEditPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: false);

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Equal("OriginalPassword", updatedLoginData.Password);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_PasswordChangedWithPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: true);

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Equal("NewPassword", updatedLoginData.Password);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_CipherKeyChangedWithPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: true, "NewKey");

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        Assert.Equal("NewKey", deps.CipherDetails.Key);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_CipherKeyChangedWithoutPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: false, "NewKey");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true));

        Assert.Contains("do not have permission", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_TotpChangedWithoutPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: false, totp: "NewTotp");

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Equal("OriginalTotp", updatedLoginData.Totp);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_TotpChangedWithPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: true, totp: "NewTotp");

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Equal("NewTotp", updatedLoginData.Totp);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_Fido2CredentialsChangedWithoutPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var passkeys = new[]
        {
            new CipherLoginFido2CredentialData
            {
                CredentialId = "CredentialId",
                UserHandle = "UserHandle",
            }
        };

        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: false, passkeys: passkeys);

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Empty(updatedLoginData.Fido2Credentials);
    }

    [Theory, BitAutoData]
    public async Task SaveDetailsAsync_Fido2CredentialsChangedWithPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var passkeys = new[]
        {
            new CipherLoginFido2CredentialData
            {
                CredentialId = "CredentialId",
                UserHandle = "UserHandle",
            }
        };

        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: true, passkeys: passkeys);

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Equal(passkeys.Length, updatedLoginData.Fido2Credentials.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveDetailsAsync_HiddenFieldsChangedWithoutPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: false, editPermission: false, fields:
        [
            new CipherFieldData
            {
                Name = "FieldName",
                Value = "FieldValue",
                Type = FieldType.Hidden,
            }
        ]);

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Empty(updatedLoginData.Fields);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveDetailsAsync_HiddenFieldsChangedWithPermission(string _, SutProvider<CipherService> sutProvider)
    {
        var deps = GetSaveDetailsAsyncDependencies(sutProvider, "NewPassword", viewPassword: true, editPermission: true, fields:
        [
            new CipherFieldData
            {
                Name = "FieldName",
                Value = "FieldValue",
                Type = FieldType.Hidden,
            }
        ]);

        await deps.SutProvider.Sut.SaveDetailsAsync(
            deps.CipherDetails,
            deps.CipherDetails.UserId.Value,
            deps.CipherDetails.RevisionDate,
            null,
            true);

        var updatedLoginData = JsonSerializer.Deserialize<CipherLoginData>(deps.CipherDetails.Data);
        Assert.Single(updatedLoginData.Fields.ToArray());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAsync_WithPersonalCipherOwner_DeletesCipher(
        Guid deletingUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.UserId = deletingUserId;
        cipherDetails.OrganizationId = null;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });

        await sutProvider.Sut.DeleteAsync(cipherDetails, deletingUserId);

        await sutProvider.GetDependency<ICipherRepository>().Received(1).DeleteAsync(cipherDetails);
        await sutProvider.GetDependency<IAttachmentStorageService>().Received(1).DeleteAttachmentsForCipherAsync(cipherDetails.Id);
        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventAsync(cipherDetails, EventType.Cipher_Deleted);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCipherDeleteAsync(cipherDetails);
    }



    [Theory]
    [BitAutoData]
    public async Task DeleteAsync_WithPersonalCipherBelongingToDifferentUser_ThrowsBadRequestException(
        Guid deletingUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.UserId = Guid.NewGuid();
        cipherDetails.OrganizationId = null;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(cipherDetails, deletingUserId));

        Assert.Contains("do not have permissions", exception.Message);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
        await sutProvider.GetDependency<IAttachmentStorageService>().DidNotReceiveWithAnyArgs().DeleteAttachmentsForCipherAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCipherEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushSyncCipherDeleteAsync(default);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task DeleteAsync_WithOrgAdminOverride_DeletesCipher(
        Guid deletingUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        await sutProvider.Sut.DeleteAsync(cipherDetails, deletingUserId, true);

        await sutProvider.GetDependency<ICipherRepository>().Received(1).DeleteAsync(cipherDetails);
        await sutProvider.GetDependency<IAttachmentStorageService>().Received(1).DeleteAttachmentsForCipherAsync(cipherDetails.Id);
        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventAsync(cipherDetails, EventType.Cipher_Deleted);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCipherDeleteAsync(cipherDetails);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task DeleteAsync_WithManagePermission_DeletesCipher(
        Guid deletingUserId, CipherDetails cipherDetails, User user, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();
        cipherDetails.Edit = false;
        cipherDetails.Manage = true;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipherDetails.OrganizationId.Value)
            .Returns(new OrganizationAbility
            {
                Id = cipherDetails.OrganizationId.Value,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.DeleteAsync(cipherDetails, deletingUserId);

        await sutProvider.GetDependency<ICipherRepository>().Received(1).DeleteAsync(cipherDetails);
        await sutProvider.GetDependency<IAttachmentStorageService>().Received(1).DeleteAttachmentsForCipherAsync(cipherDetails.Id);
        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventAsync(cipherDetails, EventType.Cipher_Deleted);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCipherDeleteAsync(cipherDetails);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task DeleteAsync_WithoutManagePermission_ThrowsBadRequestException(
        Guid deletingUserId, CipherDetails cipherDetails, User user, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();
        cipherDetails.Edit = true;
        cipherDetails.Manage = false;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipherDetails.OrganizationId.Value)
            .Returns(new OrganizationAbility
            {
                Id = cipherDetails.OrganizationId.Value,
                LimitItemDeletion = true
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(cipherDetails, deletingUserId));

        Assert.Contains("do not have permissions", exception.Message);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
        await sutProvider.GetDependency<IAttachmentStorageService>().DidNotReceiveWithAnyArgs().DeleteAttachmentsForCipherAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCipherEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushSyncCipherDeleteAsync(default);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task DeleteManyAsync_WithOrgAdminOverride_DeletesCiphers(
        Guid deletingUserId, List<Cipher> ciphers, Guid organizationId, SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(ciphers);

        await sutProvider.Sut.DeleteManyAsync(cipherIds, deletingUserId, organizationId, true);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .DeleteByIdsOrganizationIdAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == cipherIds.Count() &&
                ids.All(id => cipherIds.Contains(id))), organizationId);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyAsync_WithPersonalCipherOwner_DeletesCiphers(
        Guid deletingUserId, List<CipherDetails> ciphers, SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.UserId = deletingUserId;
            cipher.OrganizationId = null;
            cipher.Edit = true;
        }

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(ciphers);

        await sutProvider.Sut.DeleteManyAsync(cipherIds, deletingUserId);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == cipherIds.Count() &&
                ids.All(id => cipherIds.Contains(id))), deletingUserId);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyAsync_WithPersonalCipherBelongingToDifferentUser_DoesNotDeleteCiphers(
        Guid deletingUserId, List<CipherDetails> ciphers, SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();
        var differentUserId = Guid.NewGuid();

        foreach (var cipher in ciphers)
        {
            cipher.UserId = differentUserId;
            cipher.OrganizationId = null;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(new List<CipherDetails>());

        await sutProvider.Sut.DeleteManyAsync(cipherIds, deletingUserId);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()), deletingUserId);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }





    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task DeleteManyAsync_WithoutManagePermission_DoesNotDeleteCiphers(
        Guid deletingUserId, List<CipherDetails> ciphers, User user, SutProvider<CipherService> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
            cipher.Edit = true;
            cipher.Manage = false;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(ciphers);
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                {
                    organizationId, new OrganizationAbility
                    {
                        Id = organizationId,
                        LimitItemDeletion = true
                    }
                }
            });

        await sutProvider.Sut.DeleteManyAsync(cipherIds, deletingUserId, organizationId);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()), deletingUserId);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task DeleteManyAsync_WithManagePermission_DeletesCiphers(
        Guid deletingUserId, List<CipherDetails> ciphers, User user, SutProvider<CipherService> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
            cipher.Edit = false;
            cipher.Manage = true;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(ciphers);
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                {
                    organizationId, new OrganizationAbility
                    {
                        Id = organizationId,
                        LimitItemDeletion = true
                    }
                }
            });

        await sutProvider.Sut.DeleteManyAsync(cipherIds, deletingUserId, organizationId);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == cipherIds.Count() &&
                ids.All(id => cipherIds.Contains(id))), deletingUserId);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }

    [Theory]
    [BitAutoData]
    public async Task SoftDeleteAsync_WithPersonalCipherOwner_SoftDeletesCipher(
        Guid deletingUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.UserId = deletingUserId;
        cipherDetails.OrganizationId = null;
        cipherDetails.DeletedDate = null;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });

        await sutProvider.Sut.SoftDeleteAsync(cipherDetails, deletingUserId);

        Assert.NotNull(cipherDetails.DeletedDate);
        Assert.Equal(cipherDetails.RevisionDate, cipherDetails.DeletedDate);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).UpsertAsync(cipherDetails);
        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventAsync(cipherDetails, EventType.Cipher_SoftDeleted);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCipherUpdateAsync(cipherDetails, null);
    }



    [Theory]
    [BitAutoData]
    public async Task SoftDeleteAsync_WithPersonalCipherBelongingToDifferentUser_ThrowsBadRequestException(
        Guid deletingUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.UserId = Guid.NewGuid();
        cipherDetails.OrganizationId = null;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SoftDeleteAsync(cipherDetails, deletingUserId));

        Assert.Contains("do not have permissions", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SoftDeleteAsync_WithAlreadySoftDeletedCipher_SkipsOperation(
        Guid deletingUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        // Set up as personal cipher owned by the deleting user
        cipherDetails.UserId = deletingUserId;
        cipherDetails.OrganizationId = null;
        cipherDetails.DeletedDate = DateTime.UtcNow.AddDays(-1);

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });

        await sutProvider.Sut.SoftDeleteAsync(cipherDetails, deletingUserId);

        await sutProvider.GetDependency<ICipherRepository>().DidNotReceive().UpsertAsync(Arg.Any<Cipher>());
        await sutProvider.GetDependency<IEventService>().DidNotReceive().LogCipherEventAsync(Arg.Any<Cipher>(), Arg.Any<EventType>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive().PushSyncCipherUpdateAsync(Arg.Any<Cipher>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task SoftDeleteAsync_WithOrgAdminOverride_SoftDeletesCipher(
        Guid deletingUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.DeletedDate = null;

        await sutProvider.Sut.SoftDeleteAsync(cipherDetails, deletingUserId, true);

        await sutProvider.GetDependency<ICipherRepository>().Received(1).UpsertAsync(cipherDetails);
        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventAsync(cipherDetails, EventType.Cipher_SoftDeleted);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCipherUpdateAsync(cipherDetails, null);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task SoftDeleteAsync_WithManagePermission_SoftDeletesCipher(
        Guid deletingUserId, CipherDetails cipherDetails, User user, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();
        cipherDetails.DeletedDate = null;
        cipherDetails.Edit = false;
        cipherDetails.Manage = true;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipherDetails.OrganizationId.Value)
            .Returns(new OrganizationAbility
            {
                Id = cipherDetails.OrganizationId.Value,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.SoftDeleteAsync(cipherDetails, deletingUserId);

        Assert.NotNull(cipherDetails.DeletedDate);
        Assert.Equal(cipherDetails.RevisionDate, cipherDetails.DeletedDate);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).UpsertAsync(cipherDetails);
        await sutProvider.GetDependency<IEventService>().Received(1).LogCipherEventAsync(cipherDetails, EventType.Cipher_SoftDeleted);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncCipherUpdateAsync(cipherDetails, null);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task SoftDeleteAsync_WithoutManagePermission_ThrowsBadRequestException(
        Guid deletingUserId, CipherDetails cipherDetails, User user, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();
        cipherDetails.DeletedDate = null;
        cipherDetails.Edit = true;
        cipherDetails.Manage = false;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipherDetails.OrganizationId.Value)
            .Returns(new OrganizationAbility
            {
                Id = cipherDetails.OrganizationId.Value,
                LimitItemDeletion = true
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SoftDeleteAsync(cipherDetails, deletingUserId));

        Assert.Contains("do not have permissions", exception.Message);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().UpsertAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCipherEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushSyncCipherUpdateAsync(default, default);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task SoftDeleteManyAsync_WithOrgAdminOverride_SoftDeletesCiphers(
        Guid deletingUserId, List<Cipher> ciphers, Guid organizationId, SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(ciphers);

        await sutProvider.Sut.SoftDeleteManyAsync(cipherIds, deletingUserId, organizationId, true);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .SoftDeleteByIdsOrganizationIdAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == cipherIds.Count() &&
                ids.All(id => cipherIds.Contains(id))), organizationId);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }

    [Theory]
    [BitAutoData]
    public async Task SoftDeleteManyAsync_WithPersonalCipherOwner_SoftDeletesCiphers(
        Guid deletingUserId, List<CipherDetails> ciphers, SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.UserId = deletingUserId;
            cipher.OrganizationId = null;
            cipher.Edit = true;
            cipher.DeletedDate = null;
        }

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(ciphers);

        await sutProvider.Sut.SoftDeleteManyAsync(cipherIds, deletingUserId, null, false);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .SoftDeleteAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == cipherIds.Count() &&
                ids.All(id => cipherIds.Contains(id))), deletingUserId);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }

    [Theory]
    [BitAutoData]
    public async Task SoftDeleteManyAsync_WithPersonalCipherBelongingToDifferentUser_DoesNotDeleteCiphers(
        Guid deletingUserId, List<CipherDetails> ciphers, SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();
        var differentUserId = Guid.NewGuid();

        foreach (var cipher in ciphers)
        {
            cipher.UserId = differentUserId;
            cipher.OrganizationId = null;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(new List<CipherDetails>());

        await sutProvider.Sut.SoftDeleteManyAsync(cipherIds, deletingUserId, null, false);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .SoftDeleteAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()), deletingUserId);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }





    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task SoftDeleteManyAsync_WithoutManagePermission_DoesNotDeleteCiphers(
        Guid deletingUserId, List<CipherDetails> ciphers, User user, SutProvider<CipherService> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
            cipher.Edit = true;
            cipher.Manage = false;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(ciphers);
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                {
                    organizationId, new OrganizationAbility
                    {
                        Id = organizationId,
                        LimitItemDeletion = true
                    }
                }
            });

        await sutProvider.Sut.SoftDeleteManyAsync(cipherIds, deletingUserId, organizationId, false);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .SoftDeleteAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()), deletingUserId);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task SoftDeleteManyAsync_WithManagePermission_SoftDeletesCiphers(
        Guid deletingUserId, List<CipherDetails> ciphers, User user, SutProvider<CipherService> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
            cipher.Edit = false;
            cipher.Manage = true;
            cipher.DeletedDate = null;
        }

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(ciphers);
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(user);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                {
                    organizationId, new OrganizationAbility
                    {
                        Id = organizationId,
                        LimitItemDeletion = true
                    }
                }
            });

        await sutProvider.Sut.SoftDeleteManyAsync(cipherIds, deletingUserId, organizationId, false);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .SoftDeleteAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == cipherIds.Count() &&
                ids.All(id => cipherIds.Contains(id))), deletingUserId);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncCiphersAsync(deletingUserId);
    }

    [Theory]
    [BitAutoData]
    public async Task SoftDeleteAsync_CallsMarkAsCompleteByCipherIds(
        Guid deletingUserId, CipherDetails cipherDetails, SutProvider<CipherService> sutProvider)
    {
        cipherDetails.UserId = deletingUserId;
        cipherDetails.OrganizationId = null;
        cipherDetails.DeletedDate = null;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });

        await sutProvider.Sut.SoftDeleteAsync(cipherDetails, deletingUserId);

        await sutProvider.GetDependency<ISecurityTaskRepository>()
            .Received(1)
            .MarkAsCompleteByCipherIds(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 1 && ids.First() == cipherDetails.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task SoftDeleteManyAsync_CallsMarkAsCompleteByCipherIds(
        Guid deletingUserId, List<CipherDetails> ciphers, SutProvider<CipherService> sutProvider)
    {
        var cipherIds = ciphers.Select(c => c.Id).ToArray();

        foreach (var cipher in ciphers)
        {
            cipher.UserId = deletingUserId;
            cipher.OrganizationId = null;
            cipher.Edit = true;
            cipher.DeletedDate = null;
        }

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(deletingUserId)
            .Returns(new User
            {
                Id = deletingUserId,
            });
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(deletingUserId)
            .Returns(ciphers);

        await sutProvider.Sut.SoftDeleteManyAsync(cipherIds, deletingUserId, null, false);

        await sutProvider.GetDependency<ISecurityTaskRepository>()
            .Received(1)
            .MarkAsCompleteByCipherIds(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == cipherIds.Length && ids.All(id => cipherIds.Contains(id))));
    }

    private async Task AssertNoActionsAsync(SutProvider<CipherService> sutProvider)
    {
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().GetManyOrganizationDetailsByOrganizationIdAsync(default);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().RestoreByIdsOrganizationIdAsync(default, default);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().RestoreByIdsOrganizationIdAsync(default, default);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs().RestoreAsync(default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCipherEventsAsync(default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushSyncCiphersAsync(default);
    }
}
