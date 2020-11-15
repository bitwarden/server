using System;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Core.Models.Data;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using System.Collections.Generic;
using Bit.Core.Test.AutoFixture;

namespace Bit.Core.Test.Services
{
    public class CipherServiceTests
    {
        [Theory, UserCipherAutoData]
        public async Task SaveAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider, Cipher cipher)
        {
            var lastKnownRevisionDate = cipher.RevisionDate.AddDays(-1);
            sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(cipher, cipher.UserId.Value, lastKnownRevisionDate));
            Assert.Contains("out of date", exception.Message);
        }

        [Theory, UserCipherAutoData]
        public async Task SaveDetailsAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider,
            CipherDetails cipherDetails)
        {
            var lastKnownRevisionDate = cipherDetails.RevisionDate.AddDays(-1);
            sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id).Returns(cipherDetails);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveDetailsAsync(cipherDetails, cipherDetails.UserId.Value, lastKnownRevisionDate));
            Assert.Contains("out of date", exception.Message);
        }

        [Theory, UserCipherAutoData]
        public async Task ShareAsync_WrongRevisionDate_Throws(SutProvider<CipherService> sutProvider, Cipher cipher,
            Organization organization, List<Guid> collectionIds)
        {
            sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var lastKnownRevisionDate = cipher.RevisionDate.AddDays(-1);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
                lastKnownRevisionDate));
            Assert.Contains("out of date", exception.Message);
        }

        [Theory]
        [InlineUserCipherAutoData("")]
        [InlineUserCipherAutoData("Correct Time")]
        public async Task SaveAsync_CorrectRevisionDate_Passes(string revisionDateString,
            SutProvider<CipherService> sutProvider, Cipher cipher)
        {
            var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
            sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

            await sutProvider.Sut.SaveAsync(cipher, cipher.UserId.Value, lastKnownRevisionDate);
            await sutProvider.GetDependency<ICipherRepository>().Received(1).ReplaceAsync(cipher);
        }

        [Theory]
        [InlineUserCipherAutoData("")]
        [InlineUserCipherAutoData("Correct Time")]
        public async Task SaveDetailsAsync_CorrectRevisionDate_Passes(string revisionDateString,
            SutProvider<CipherService> sutProvider, CipherDetails cipherDetails)
        {
            var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipherDetails.RevisionDate;
            sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id).Returns(cipherDetails);

            await sutProvider.Sut.SaveDetailsAsync(cipherDetails, cipherDetails.UserId.Value, lastKnownRevisionDate);
            await sutProvider.GetDependency<ICipherRepository>().Received(1).ReplaceAsync(cipherDetails);
        }

        [Theory]
        [InlineUserCipherAutoData("")]
        [InlineUserCipherAutoData("Correct Time")]
        public async Task ShareAsync_CorrectRevisionDate_Passes(string revisionDateString,
            SutProvider<CipherService> sutProvider, Cipher cipher, Organization organization, List<Guid> collectionIds)
        {
            var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
            var cipherRepository = sutProvider.GetDependency<ICipherRepository>();
            cipherRepository.GetByIdAsync(cipher.Id).Returns(cipher);
            cipherRepository.ReplaceAsync(cipher, collectionIds).Returns(true);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

            await sutProvider.Sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
                lastKnownRevisionDate);
            await cipherRepository.Received(1).ReplaceAsync(cipher, collectionIds);
        }
    }
}
