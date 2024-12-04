using System.Reflection;
using Bit.Infrastructure.IntegrationTest.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseDataAttribute : DataAttribute
{
    public bool UseFakeTimeProvider { get; set; }

    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var customizers = GetOrderedCustomizers(testMethod);

        var databases = DatabaseStartup.Databases;

        if (databases == null)
        {
            throw new InvalidOperationException("Databases wasn't supplied, this likely means DatabaseStartup didn't run.");
        }

        var theories = new ITheoryDataRow[databases.Count];

        for (var i = 0; i < theories.Length; i++)
        {
            var customizationContext = new CustomizationContext(databases[i] with {}, testMethod, disposalTracker);
            foreach (var customizer in customizers)
            {
                await customizer.CustomizeAsync(customizationContext);
            }

            var isEnabled = customizationContext.Enabled ?? customizationContext.Database.Enabled;

            TheoryDataRowBase theory;

            if (!isEnabled)
            {
                theory = new TheoryDataRow()
                    .WithSkip("Not Enabled");
            }
            else
            {
                theory = new ServiceTheoryDataRow(testMethod, disposalTracker, customizationContext.Services.BuildServiceProvider());
            }

            theory
                .WithTrait("Type", customizationContext.Database.Type.ToString())
                .WithTrait("ConnectionString", customizationContext.Database.ConnectionString ?? "(none)")
                .WithTestDisplayName($"{testMethod.Name}[{customizationContext.Database.Name ?? customizationContext.Database.Type.ToString()}]");

            theories[i] = theory;
        }

        return theories;
    }

    public override bool SupportsDiscoveryEnumeration()
    {
        return true;
    }

    private static IEnumerable<TestCustomizerAttribute> GetOrderedCustomizers(MethodInfo methodInfo)
    {
        var assemblyAttributes = methodInfo.DeclaringType?.Assembly.GetCustomAttributes<TestCustomizerAttribute>() ?? [];
        var typeAttributes = methodInfo.DeclaringType?.GetCustomAttributes<TestCustomizerAttribute>() ?? [];
        var methodAttributes = methodInfo.GetCustomAttributes<TestCustomizerAttribute>();

        IReadOnlyCollection<TestCustomizerAttribute> allAttributes = [..assemblyAttributes, ..typeAttributes, ..methodAttributes];

        if (allAttributes.Count == 0)
        {
            return [DefaultCustomizerAttribute.Instance];
        }

        return allAttributes;
    }
}
