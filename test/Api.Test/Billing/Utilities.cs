using Bit.Api.Billing.Controllers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using NSubstitute;

namespace Bit.Api.Test.Billing;

public static class Utilities
{
    public static void ConfigureStableAdminInputs<T>(
        Provider provider,
        SutProvider<T> sutProvider) where T : BaseProviderController
    {
        ConfigureBaseInputs(provider, sutProvider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(true);
    }

    public static void ConfigureStableServiceUserInputs<T>(
        Provider provider,
        SutProvider<T> sutProvider) where T : BaseProviderController
    {
        ConfigureBaseInputs(provider, sutProvider);

        sutProvider.GetDependency<ICurrentContext>().ProviderUser(provider.Id)
            .Returns(true);
    }

    private static void ConfigureBaseInputs<T>(
        Provider provider,
        SutProvider<T> sutProvider) where T : BaseProviderController
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        provider.Type = ProviderType.Msp;
        provider.Status = ProviderStatusType.Billable;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
    }
}
