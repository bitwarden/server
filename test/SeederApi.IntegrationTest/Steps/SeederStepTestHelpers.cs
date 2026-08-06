using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.SeederApi.IntegrationTest.Steps;

/// <summary>
/// Shared harness for step-level tests under <c>Steps/</c>. Provides a minimal in-memory
/// <see cref="SeederContext"/> wired with the services every step needs, plus a stub
/// <see cref="ISeedReader"/> for tests that load fixtures by name.
/// </summary>
internal static class SeederStepTestHelpers
{
    internal const string TestDomain = "test.example";
    internal const string TestOrgName = "Test Org";

    internal static SeederContext NewContext(SeederSettings settings, ISeedReader? reader = null)
        => NewContextWithMangler(settings, new NoOpManglerService(), reader);

    internal static SeederContext NewContextWithMangler(
        SeederSettings settings, IManglerService mangler, ISeedReader? reader = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton(mangler);
        services.AddSingleton(settings);
        if (reader is not null)
        {
            services.AddSingleton(reader);
        }
        return new SeederContext(services.BuildServiceProvider());
    }

    internal static void PreloadOrganization(SeederContext context, string name = TestOrgName, string domain = TestDomain)
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var org = OrganizationSeeder.Create(
            name, domain, seats: 10, context.GetMangler(),
            orgKeys.PublicKey, orgKeys.PrivateKey, PlanType.EnterpriseAnnually);
        context.Organization = org;
        context.OrgKeys = orgKeys;
        context.Domain = domain;
        context.Organizations.Add(org);
    }

    internal sealed class StubSeedReader : ISeedReader
    {
        private readonly Dictionary<string, object> _seeds = new();

        public StubSeedReader Add<T>(string name, T value)
        {
            _seeds[name] = value!;
            return this;
        }

        public T Read<T>(string seedName) => (T)_seeds[seedName];

        public IReadOnlyList<string> ListAvailable() => _seeds.Keys.ToArray();

        public byte[] ReadBytes(string fileName) =>
            throw new NotSupportedException("StubSeedReader does not provide binary samples.");
    }
}
