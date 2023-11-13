using Bit.Core.Auth.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class GrantRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetByKeyAsync(IGrantRepository grantRepository)
    {
        var grant = await grantRepository.GetByKeyAsync("key");

        Assert.NotNull(grant);
    }
}
