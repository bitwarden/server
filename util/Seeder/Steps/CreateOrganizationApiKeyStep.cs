using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

internal sealed class CreateOrganizationApiKeyStep : IStep
{
    public void Execute(SeederContext context)
    {
        var org = context.RequireOrganization();

        var apiKey = new OrganizationApiKey
        {
            Id = CombGuid.Generate(),
            OrganizationId = org.Id,
            Type = OrganizationApiKeyType.Default,
            ApiKey = CoreHelpers.SecureRandomString(30),
            RevisionDate = DateTime.UtcNow,
        };

        context.OrganizationApiKey = apiKey;
    }
}
