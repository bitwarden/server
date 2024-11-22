using System.Reflection;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper;
using Bit.Infrastructure.EntityFramework;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.IntegrationTest.Services;
using Bit.Infrastructure.IntegrationTest.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseDataAttribute : DataAttribute
{
    public bool SelfHosted { get; set; }
    public bool UseFakeTimeProvider { get; set; }
    public string? MigrationName { get; set; }

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var builders = DatabaseStartup.Builders;

        if (builders == null)
        {
            throw new InvalidOperationException("Builders wasn't supplied, this likely means DatabaseStartup didn't run.");
        }

        var theoryData = new ITheoryDataRow[builders.Count];
        for (var i = 0; i < builders.Count; i++)
        {
            theoryData[i] = builders[i](testMethod, disposalTracker, this);
        }
        return new(theoryData);
    }

    public override bool SupportsDiscoveryEnumeration()
    {
        return true;
    }
}
