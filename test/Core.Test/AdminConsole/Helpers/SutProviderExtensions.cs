using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using NSubstitute;

namespace Bit.Core.Test.AdminConsole.Helpers;

public static class SutProviderExtensions
{
    public static void EnableFeatureFlag<T>(this SutProvider<T> sutProvider, string featureFlag)
        => sutProvider.GetDependency<IFeatureService>().IsEnabled(featureFlag).Returns(true);
}
