using Bit.Commercial.Core.SecretsManager.Queries.ServiceAccounts;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Queries.ServiceAccounts;

[SutProviderCustomize]
public class ServiceAccountSecretsDetailsQueryTests
{
    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async Task GetManyByOrganizationId_CallsDifferentRepoMethods(
        bool includeAccessToSecrets,
        SutProvider<ServiceAccountSecretsDetailsQuery> sutProvider,
        Guid organizationId,
        Guid userId,
        AccessClientType accessClient,
        ServiceAccount mockSa,
        ServiceAccountSecretsDetails mockSaDetails)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetManyByOrganizationIdAsync(default, default, default)
            .ReturnsForAnyArgs(new List<ServiceAccount> { mockSa });

        sutProvider.GetDependency<IServiceAccountRepository>().GetManyByOrganizationIdWithSecretsDetailsAsync(default, default, default)
            .ReturnsForAnyArgs(new List<ServiceAccountSecretsDetails> { mockSaDetails });


        var result = await sutProvider.Sut.GetManyByOrganizationIdAsync(organizationId, userId, accessClient, includeAccessToSecrets);

        if (includeAccessToSecrets)
        {
            await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
                .GetManyByOrganizationIdWithSecretsDetailsAsync(Arg.Is(AssertHelper.AssertPropertyEqual(mockSaDetails.ServiceAccount.OrganizationId)),
                    Arg.Any<Guid>(), Arg.Any<AccessClientType>());
        }
        else
        {
            await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
                .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(mockSa.OrganizationId)),
                    Arg.Any<Guid>(), Arg.Any<AccessClientType>());
            Assert.Equal(0, result.First().AccessToSecrets);
        }
    }
}
