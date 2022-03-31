using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{

    [SutProviderCustomize]
    public class CloudSyncSponsorshipsCommandTests : CancelSponsorshipCommandTestsBase
    {

        [Theory]
        [BitAutoData]
        public async Task SyncOrganization_SponsoringOrgNotFound_ThrowsBadRequest(
            IEnumerable<OrganizationSponsorshipData> sponsorshipsData,
            SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SyncOrganization(null, sponsorshipsData));

            Assert.Contains("Failed to sync sponsorship - missing organization.", exception.Message);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertManyAsync(default);
            await sutProvider.GetDependency<ISendSponsorshipOfferCommand>()
                .DidNotReceiveWithAnyArgs()
                .BulkSendSponsorshipOfferAsync(default, default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .DeleteManyAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task SyncOrganization_NoSponsorships_ThrowsBadRequest(
            Organization organization,
            SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SyncOrganization(organization, null));

            Assert.Contains("Failed to sync sponsorship - missing sponsorships.", exception.Message);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertManyAsync(default);
            await sutProvider.GetDependency<ISendSponsorshipOfferCommand>()
                .DidNotReceiveWithAnyArgs()
                .BulkSendSponsorshipOfferAsync(default, default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .DeleteManyAsync(default);
        }
    }
}
