using System;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;
using Bit.Core.Exceptions;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Context;
using Organization = Bit.Core.Models.Table.Organization;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Services
{
    public class OrganizationServiceTests
    {
         [Theory, CustomAutoData(typeof(SutProviderCustomization))]
         public async Task UpdateOrganizationKeysAsync_WithoutManageResetPassword_Throws(Guid orgId, string publicKey,
             string privateKey, SutProvider<OrganizationService> sutProvider)
         {
             var currentContext = Substitute.For<ICurrentContext>();
             currentContext.ManageResetPassword(orgId).Returns(false);

             await Assert.ThrowsAsync<UnauthorizedAccessException>(
                 () => sutProvider.Sut.UpdateOrganizationKeysAsync(orgId, publicKey, privateKey));
         }

         [Theory, CustomAutoData(typeof(SutProviderCustomization))]
         public async Task UpdateOrganizationKeysAsync_KeysAlreadySet_Throws(Organization org, string publicKey,
             string privateKey, SutProvider<OrganizationService> sutProvider)
         {
             var currentContext = sutProvider.GetDependency<ICurrentContext>();
             currentContext.ManageResetPassword(org.Id).Returns(true);

             var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
             organizationRepository.GetByIdAsync(org.Id).Returns(org);

             var exception = await Assert.ThrowsAsync<BadRequestException>(
                 () => sutProvider.Sut.UpdateOrganizationKeysAsync(org.Id, publicKey, privateKey));
             Assert.Contains("Organization Keys already exist", exception.Message);
         }

         [Theory, CustomAutoData(typeof(SutProviderCustomization))]
         public async Task UpdateOrganizationKeysAsync_KeysAlreadySet_Success(Organization org, string publicKey,
             string privateKey, SutProvider<OrganizationService> sutProvider)
         {
             org.PublicKey = null;
             org.PrivateKey = null;

             var currentContext = sutProvider.GetDependency<ICurrentContext>();
             currentContext.ManageResetPassword(org.Id).Returns(true);

             var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
             organizationRepository.GetByIdAsync(org.Id).Returns(org);

             await sutProvider.Sut.UpdateOrganizationKeysAsync(org.Id, publicKey, privateKey);
         }
         
         [Theory, PaidOrganizationAutoData]
         public async Task Delete_Success(Organization organization, SutProvider<OrganizationService> sutProvider)
         {
             var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
             var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();

             await sutProvider.Sut.DeleteAsync(organization);

             await organizationRepository.Received().DeleteAsync(organization);
             await applicationCacheService.Received(). DeleteOrganizationAbilityAsync(organization.Id);
         }

         [Theory, PaidOrganizationAutoData]
         public async Task Delete_Fails_KeyConnector(Organization organization, SutProvider<OrganizationService> sutProvider,
             SsoConfig ssoConfig)
         {
             ssoConfig.Enabled = true;
             ssoConfig.SetData(new SsoConfigurationData { KeyConnectorEnabled = true });
             var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
             var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
             var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();

             ssoConfigRepository.GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

             var exception = await Assert.ThrowsAsync<BadRequestException>(
                 () => sutProvider.Sut.DeleteAsync(organization));

             Assert.Contains("You cannot delete an Organization that is using Key Connector.", exception.Message);

             await organizationRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
             await applicationCacheService.DidNotReceiveWithAnyArgs().DeleteOrganizationAbilityAsync(default);
         }
    }
}
