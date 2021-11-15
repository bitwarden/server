using AutoFixture.Xunit2;
using System.Threading.Tasks;
using Xunit;
using Bit.Core.Test.AutoFixture;
using Bit.Api.Controllers;
using System;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using NSubstitute;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Api.Test.Controllers
{
    public class OrganizationsControllerTests
    {
        [Theory, AutoData]
        public async Task OrganizationsController_WhenUserTriestoLeaveOrganizationUsingKeyConnector_Throws(
            Guid orgId, SutProvider<OrganizationsController> sutProvider)
        {
            var ssoConfig = new SsoConfig
            {
                Id = default,
                Data = "{\"useKeyConnector\": true}",
                Enabled = true,
                OrganizationId = orgId,
            };

            sutProvider.GetDependency<ISsoConfigRepository>()
                .GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.Leave(orgId.ToString()));

            Assert.Contains("You cannot leave an Organization that is using Key Connector.",
                exception.Message);

            await sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs()
                .DeleteUserAsync(default, default);
        }
    }
}

