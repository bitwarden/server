using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Seeder.Recipes;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class ProviderOrganizationsControllerPerformanceTest(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task DeleteProviderOrganizationAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        factory.SubstituteService<Bit.Core.Services.IStripeAdapter>(stripe =>
        {
            stripe.SubscriptionGetAsync(Arg.Any<string>(), Arg.Any<Stripe.SubscriptionGetOptions>())
                .Returns(new Stripe.Subscription { Id = "sub_test", Status = "active", CustomerId = "cus_test", Customer = new Stripe.Customer() });

            stripe.CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<Stripe.CustomerUpdateOptions>())
                .Returns(new Stripe.Customer { Id = "cus_test" });

            stripe.CustomerDeleteDiscountAsync(Arg.Any<string>(), Arg.Any<Stripe.CustomerDeleteDiscountOptions>())
                .Returns(Task.CompletedTask);

            stripe.SubscriptionUpdateAsync(Arg.Any<string>(), Arg.Any<Stripe.SubscriptionUpdateOptions>())
                .Returns(ci => new Stripe.Subscription { Id = ci.ArgAt<string>(0) });

            stripe.SubscriptionCreateAsync(Arg.Any<Stripe.SubscriptionCreateOptions>())
                .Returns(new Stripe.Subscription { Id = $"sub_{Guid.NewGuid():N}" });
        });

        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var providerRepository = factory.GetService<IProviderRepository>();

        var domain = $"providerorg.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Name = "Test Provider",
            Enabled = true,
            Type = ProviderType.Msp
        };
        await providerRepository.CreateAsync(provider);

        var providerOrganizationRepository = factory.GetService<IProviderOrganizationRepository>();
        var providerOrganization = new ProviderOrganization
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            OrganizationId = orgId,
            Key = "test-key"
        };
        await providerOrganizationRepository.CreateAsync(providerOrganization);

        var providerUserRepository = factory.GetService<IProviderUserRepository>();
        var providerUser = new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            UserId = db.Users.First(u => u.Email == $"owner@{domain}").Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        };
        await providerUserRepository.CreateAsync(providerUser);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.DeleteAsync($"/providers/{provider.Id}/organizations/{providerOrganization.Id}");

        stopwatch.Stop();
        testOutputHelper.WriteLine($"DELETE /providers/{{providerid}}/organizations/{{id}} - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        var result = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

