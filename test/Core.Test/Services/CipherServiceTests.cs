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
using AutoFixture;
using Bit.Core.Test.AutoFixture;

namespace Bit.Core.Test.Services
{
    public class CipherServiceTests
    {
        [Theory, UserCipherAutoData]
        public async Task SaveAsync_WrongRevisionDate_Throws(Cipher cipher)
        {
            var lastKnownRevisionDate = cipher.RevisionDate.AddDays(-1);
            var sut = new Fixture().WithAutoNSubstitutions()
                                   .For<CipherService>()
                                   .Freeze(out ICipherRepository cipherRepository)
                                   .Create();
            cipherRepository.GetByIdAsync(cipher.Id).Returns(cipher);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sut.SaveAsync(cipher, cipher.UserId.Value, lastKnownRevisionDate));
            Assert.Contains("out of date", exception.Message);
        }

        [Theory, UserCipherAutoData]
        public async Task SaveDetailsAsync_WrongRevisionDate_Throws(CipherDetails cipherDetails)
        {
            var lastKnownRevisionDate = cipherDetails.RevisionDate.AddDays(-1);
            var sut = new Fixture().WithAutoNSubstitutions()
                                   .For<CipherService>()
                                   .Freeze(out ICipherRepository cipherRepository)
                                   .Create();
            cipherRepository.GetByIdAsync(cipherDetails.Id).Returns(cipherDetails);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sut.SaveDetailsAsync(cipherDetails, cipherDetails.UserId.Value, lastKnownRevisionDate));
            Assert.Contains("out of date", exception.Message);
        }

        [Theory, UserCipherAutoData]
        public async Task ShareAsync_WrongRevisionDate_Throws(Cipher cipher, Organization organization,
            List<Guid> collectionIds)
        {
            var sut = new Fixture().WithAutoNSubstitutions()
                                   .For<CipherService>()
                                   .Freeze(out ICipherRepository cipherRepository)
                                   .Freeze(out IOrganizationRepository organizationRepository)
                                   .Create();
            cipherRepository.GetByIdAsync(cipher.Id).Returns(cipher);
            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

            var lastKnownRevisionDate = cipher.RevisionDate.AddDays(-1);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
                    lastKnownRevisionDate));
            Assert.Contains("out of date", exception.Message);
        }

        [Theory]
        [InlineUserCipherAutoData("")]
        [InlineUserCipherAutoData("Correct Time")]
        public async Task SaveAsync_CorrectRevisionDate_Passes(string revisionDateString, Cipher cipher)
        {
            var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
            var sut = new Fixture().WithAutoNSubstitutions()
                                   .For<CipherService>()
                                   .Freeze(out ICipherRepository cipherRepository)
                                   .Create();
            cipherRepository.GetByIdAsync(cipher.Id).Returns(cipher);

            await sut.SaveAsync(cipher, cipher.UserId.Value, lastKnownRevisionDate);
            await cipherRepository.Received(1).ReplaceAsync(cipher);
        }

        [Theory]
        [InlineUserCipherAutoData("")]
        [InlineUserCipherAutoData("Correct Time")]
        public async Task SaveDetailsAsync_CorrectRevisionDate_Passes(string revisionDateString, CipherDetails cipherDetails)
        {
            var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipherDetails.RevisionDate;
            var sut = new Fixture().WithAutoNSubstitutions()
                                   .For<CipherService>()
                                   .Freeze(out ICipherRepository cipherRepository)
                                   .Create();
            cipherRepository.GetByIdAsync(cipherDetails.Id).Returns(cipherDetails);

            await sut.SaveDetailsAsync(cipherDetails, cipherDetails.UserId.Value, lastKnownRevisionDate);
            await cipherRepository.Received(1).ReplaceAsync(cipherDetails);
        }

        [Theory]
        [InlineUserCipherAutoData("")]
        [InlineUserCipherAutoData("Correct Time")]
        public async Task ShareAsync_CorrectRevisionDate_Passes(string revisionDateString, Cipher cipher,
            Organization organization, List<Guid> collectionIds)
        {
            var lastKnownRevisionDate = string.IsNullOrEmpty(revisionDateString) ? (DateTime?)null : cipher.RevisionDate;
            var sut = new Fixture().WithAutoNSubstitutions()
                                   .For<CipherService>()
                                   .Freeze(out ICipherRepository cipherRepository)
                                   .Freeze(out IOrganizationRepository organizationRepository)
                                   .Create();
            cipherRepository.GetByIdAsync(cipher.Id).Returns(cipher);
            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            cipherRepository.ReplaceAsync(cipher, collectionIds).Returns(true);


            await sut.ShareAsync(cipher, cipher, organization.Id, collectionIds, cipher.UserId.Value,
                    lastKnownRevisionDate);
            await cipherRepository.Received(1).ReplaceAsync(cipher, collectionIds);
        }
    }
}
