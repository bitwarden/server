using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.IntegrationTest;

public static class ServiceProviderExtensions
{
    public static async Task<User> CreateUserAsync(this IServiceProvider services)
    {
        var userRepository = services.GetRequiredService<IUserRepository>();
        return await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Key = "something",
            PrivateKey = "private_key",
        });
    }

    public static async Task<Organization> CreateOrganizationAsync(this IServiceProvider services)
    {
        var organizationRepository = services.GetRequiredService<IOrganizationRepository>();
        return await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org", // TODO: EF doesn't enforce this as not null
            BillingEmail = "email@test.com", // TODO: EF doesn't enforce this as not null
            Plan = "Free", // TODO: EF doesn't enforce this as not null
        });
    }

    public static async Task<OrganizationUser> CreateOrganizationUserAsync(this IServiceProvider services)
    {
        var organizationUserRepository = services.GetRequiredService<IOrganizationUserRepository>();

        var createdUser = await services.CreateUserAsync();
        var createdOrganization = await services.CreateOrganizationAsync();

        return await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = createdUser.Id,
            OrganizationId = createdOrganization.Id,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Confirmed,
        });
    }

    public static async Task<Organization> UpdateOrganizationAsync(this IServiceProvider services, Guid organizationId, Action<Organization> configure)
    {
        var organizationRepository = services.GetRequiredService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(organizationId);
        configure(organization);
        await organizationRepository.ReplaceAsync(organization);
        return organization;
    }

    public static async Task<IReadOnlyList<T>> CreateManyAsync<TService, T>(this IServiceProvider services, Func<TService, int, Task<T>> creator, int number)
        where TService : class
    {
        var service = services.GetRequiredService<TService>();

        var items = new T[number];
        for (var i = 0; i < number; i++)
        {
            items[i] = await creator(service, i);
        }
        return items;
    }

    public static async Task<IReadOnlyList<T>> CreateManyAsync<TService, T>(this IServiceProvider services, Func<TService, Task<T>> creator, int number)
        where TService : class
        => await services.CreateManyAsync(async (TService service, int _) => await creator(service), number);
}
