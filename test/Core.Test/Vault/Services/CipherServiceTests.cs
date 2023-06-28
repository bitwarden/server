using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Castle.Core.Internal;
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
        IEnumerable<Cipher> ciphers, Guid organizationId, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(new Organization
            {
                PlanType = Enums.PlanType.EnterpriseAnnually,
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
        SutProvider<CipherService> sutProvider, IEnumerable<Cipher> ciphers, Organization organization, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id)
            .Returns(new Organization
            {
                PlanType = Enums.PlanType.EnterpriseAnnually,
                MaxStorageGb = 100
            });

        var cipherInfos = ciphers.Select(c => (c,
            string.IsNullOrEmpty(revisionDateString) ? null : (DateTime?)c.RevisionDate));
        var sharingUserId = ciphers.First().UserId.Value;

        await sutProvider.Sut.ShareManyAsync(cipherInfos, organization.Id, collectionIds, sharingUserId);
        await sutProvider.GetDependency<ICipherRepository>().Received(1).UpdateCiphersAsync(sharingUserId,
            Arg.Is<IEnumerable<Cipher>>(arg => arg.Except(ciphers).IsNullOrEmpty()));
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreAsync_UpdatesUserCipher(Guid restoringUserId, Cipher cipher, SutProvider<CipherService> sutProvider)
    {
        sutProvider.GetDependency<ICipherRepository>().GetCanEditByIdAsync(restoringUserId, cipher.Id).Returns(true);

        var initialRevisionDate = new DateTime(1970, 1, 1, 0, 0, 0);
        cipher.DeletedDate = initialRevisionDate;
        cipher.RevisionDate = initialRevisionDate;

        await sutProvider.Sut.RestoreAsync(cipher, restoringUserId, cipher.OrganizationId.HasValue);

        Assert.Null(cipher.DeletedDate);
        Assert.NotEqual(initialRevisionDate, cipher.RevisionDate);
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public async Task RestoreAsync_UpdatesOrganizationCipher(Guid restoringUserId, Cipher cipher, SutProvider<CipherService> sutProvider)
    {
        sutProvider.GetDependency<ICipherRepository>().GetCanEditByIdAsync(restoringUserId, cipher.Id).Returns(true);

        var initialRevisionDate = new DateTime(1970, 1, 1, 0, 0, 0);
        cipher.DeletedDate = initialRevisionDate;
        cipher.RevisionDate = initialRevisionDate;

        await sutProvider.Sut.RestoreAsync(cipher, restoringUserId, cipher.OrganizationId.HasValue);

        Assert.Null(cipher.DeletedDate);
        Assert.NotEqual(initialRevisionDate, cipher.RevisionDate);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreManyAsync_UpdatesCiphers(IEnumerable<CipherDetails> ciphers,
        SutProvider<CipherService> sutProvider)
    {
        var restoringUserId = ciphers.First().UserId.Value;
        var previousRevisionDate = DateTime.UtcNow;
        foreach (var cipher in ciphers)
        {
            cipher.RevisionDate = previousRevisionDate;
        }

        var revisionDate = previousRevisionDate + TimeSpan.FromMinutes(1);
        sutProvider.GetDependency<ICipherRepository>().RestoreAsync(Arg.Any<IEnumerable<Guid>>(), restoringUserId)
            .Returns(revisionDate);

        await sutProvider.Sut.RestoreManyAsync(ciphers, restoringUserId);

        foreach (var cipher in ciphers)
        {
            Assert.Null(cipher.DeletedDate);
            Assert.Equal(revisionDate, cipher.RevisionDate);
        }
    }

    [Theory, BitAutoData]
    public async Task ShareManyAsync_FreeOrgWithAttachment_Throws(SutProvider<CipherService> sutProvider,
        IEnumerable<Cipher> ciphers, Guid organizationId, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(new Organization
        {
            PlanType = Enums.PlanType.Free
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
        IEnumerable<Cipher> ciphers, Guid organizationId, List<Guid> collectionIds)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(new Organization
            {
                PlanType = Enums.PlanType.EnterpriseAnnually,
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
            Arg.Is<IEnumerable<Cipher>>(arg => arg.Except(ciphers).IsNullOrEmpty()));
    }
}
