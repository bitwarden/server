using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Castle.Core.Internal;
using Core.Models.Data;
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

        await sutProvider.Sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
            lastKnownRevisionDate);
        await cipherRepository.Received(1).ReplaceAsync(cipher, collectionIds);
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
