using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

internal sealed class CreateOrganizationDomainsStep(IReadOnlyList<string> domainNames) : IStep
{
    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();

        foreach (var name in domainNames)
        {
            context.OrganizationDomains.Add(OrganizationDomainSeeder.Create(orgId, name));
        }
    }
}
