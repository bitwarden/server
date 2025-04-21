using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest;

[assembly: SeedConfiguration<SimpleSeeder>("Simple")]

namespace Bit.Infrastructure.IntegrationTest;

// TODO: We could implement IAsyncDisposable to cleanup the seeded data
public class SimpleSeeder : ISeeder
{
    private readonly IUserRepository _userRepository;

    public SimpleSeeder(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task SeedAsync(SeedContext context)
    {
        var user = await _userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        context.Set("User", user);
    }
}
