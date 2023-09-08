using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class PolicyRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_Works(IPolicyRepository policyRepository,
        IServiceProvider services)
    {
        var organizationUser = await services.CreateOrganizationUserAsync();

        var createdPolicy = await policyRepository.CreateAsync(new Policy
        {
            Type = PolicyType.ActivateAutofill,
            Enabled = true,
            Data = "{}",
            OrganizationId = organizationUser.OrganizationId,
        });

        // Assert that we get the Id back right away so that we know we set the Id client side
        Assert.NotEqual(createdPolicy.Id, Guid.Empty);

        // Assert that we can find one from the database
        var policy = await policyRepository.GetByIdAsync(createdPolicy.Id);
        Assert.NotNull(policy);

        // Assert the found item has all the data we expect
        Assert.Equal(PolicyType.ActivateAutofill, policy.Type);
        Assert.True(policy.Enabled);
        Assert.Equal("{}", policy.Data);
        Assert.Equal(organizationUser.OrganizationId, policy.OrganizationId);

        // Assert the items returned from CreateAsync match what is found by id
        Assert.Equal(createdPolicy.Id, policy.Id);
        Assert.Equal(createdPolicy.Type, policy.Type);
        Assert.Equal(createdPolicy.Enabled, policy.Enabled);
        Assert.Equal(createdPolicy.Data, policy.Data);
        Assert.Equal(createdPolicy.OrganizationId, policy.OrganizationId);
    }
}
