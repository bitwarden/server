using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.ServiceAccounts;

[SutProviderCustomize]
public class AutoscaleServiceAccountsCommandTests
{
    [Theory, BitAutoData]
    public async Task AutoscaleServiceAccountsAsync_Success(
        int serviceAccountsToAdd,
        Guid organizationId,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        await sutProvider.Sut.AutoscaleServiceAccountsAsync(organizationId, serviceAccountsToAdd);

        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .Received(1)
            .UpdateSecretsManagerSubscription(Arg.Is<SecretsManagerSubscriptionUpdate>(s =>
                s.OrganizationId == organizationId &&
                s.SmServiceAccountsAdjustment == serviceAccountsToAdd));
    }
}
