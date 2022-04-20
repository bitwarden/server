﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{

    [SutProviderCustomize]
    public class CloudSyncSponsorshipsCommandTests : FamiliesForEnterpriseTestsBase
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
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .DeleteManyAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task SyncOrganization_NoSponsorships_EarlyReturn(
            Organization organization,
            SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
        {
            var result = await sutProvider.Sut.SyncOrganization(organization, Enumerable.Empty<OrganizationSponsorshipData>());

            Assert.Empty(result.Item1.SponsorshipsBatch);
            Assert.Empty(result.Item2);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertManyAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .DeleteManyAsync(default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task SyncOrganization_BadSponsoringOrgPlan_NoSync(
            PlanType planType,
            Organization organization, IEnumerable<OrganizationSponsorshipData> sponsorshipsData,
            SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
        {
            organization.PlanType = planType;

            await sutProvider.Sut.SyncOrganization(organization, sponsorshipsData);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertManyAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .DeleteManyAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task SyncOrganization_Success_RecordsEvent(Organization organization, Guid installationId,
            SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);
            await sutProvider.Sut.SyncOrganization(organization, Array.Empty<OrganizationSponsorshipData>());

            await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationEventAsync(organization, EventType.Organization_SponsorshipsSynced, Arg.Any<DateTime?>(), installationId);
        }

    }
}
