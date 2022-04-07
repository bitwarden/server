
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using Moq;
using NSubstitute;
using RichardSzalay.MockHttp;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{

    public class SelfHostedSyncSponsorshipsCommandTests : FamiliesForEnterpriseTestsBase
    {

        public static SutProvider<SelfHostedSyncSponsorshipsCommand> GetSutProvider(bool enableCloudCommunication = true)
        {
            var fixture = new Fixture().WithAutoNSubstitutionsAutoPopulatedProperties();
            fixture.AddMockHttp();

            var settings = fixture.Create<IGlobalSettings>();
            settings.SelfHosted = true;
            settings.EnableCloudCommunication = enableCloudCommunication;
            var internalUri = fixture.Create<Uri>();
            var identityUri = fixture.Create<Uri>();
            settings.BaseServiceUri.InternalVault = internalUri.ToString();
            settings.BaseServiceUri.InternalIdentity = identityUri.ToString();

            var handler = new MockHttpMessageHandler();
            var uri = internalUri.ToString() + "organizationSponsorships/sync";
            handler.When(HttpMethod.Post, uri)
                .Respond(HttpStatusCode.OK, new StringContent("hi"));

            var http = handler.ToHttpClient();

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(p => p.CreateClient(It.IsAny<string>())).Returns(http);

            return new SutProvider<SelfHostedSyncSponsorshipsCommand>(fixture)
                .SetDependency(settings)
                .SetDependency(mockHttpClientFactory.Object)
                .Create();
        }

        [Theory]
        [BitAutoData]
        public async Task SyncOrganization_BillingSyncKeyDisabled_ThrowsBadRequest(
            Guid organizationId, Guid cloudOrganizationId, OrganizationConnection billingSyncConnection)
        {
            var sutProvider = GetSutProvider();
            billingSyncConnection.Enabled = false;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SyncOrganization(organizationId, cloudOrganizationId, billingSyncConnection));

            Assert.Contains($"Billing Sync Key disabled for organization {organizationId}", exception.Message);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .DeleteManyAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertManyAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task SyncOrganization_BillingSyncKeyEmpty_ThrowsBadRequest(
            Guid organizationId, Guid cloudOrganizationId, OrganizationConnection billingSyncConnection)
        {
            var sutProvider = GetSutProvider();
            billingSyncConnection.Config = "";

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SyncOrganization(organizationId, cloudOrganizationId, billingSyncConnection));

            Assert.Contains($"No Billing Sync Key known for organization {organizationId}", exception.Message);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .DeleteManyAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertManyAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task SyncOrganization_CloudCommunicationDisabled_EarlyReturn(
            Guid organizationId, Guid cloudOrganizationId, OrganizationConnection billingSyncConnection)
        {
            var sutProvider = GetSutProvider(false);

            await sutProvider.Sut.SyncOrganization(organizationId, cloudOrganizationId, billingSyncConnection);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .DeleteManyAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertManyAsync(default);
        }
    }
}
