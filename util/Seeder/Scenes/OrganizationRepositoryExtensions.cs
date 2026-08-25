using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;

namespace Bit.Seeder.Scenes;

public static class OrganizationRepositoryExtensions
{
    public static async Task<Organization> GetSecretsManagerOrganizationOrThrowAsync(
        this IOrganizationRepository organizationRepository, Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {organizationId} not found.");
        }

        if (!organization.UseSecretsManager)
        {
            throw new InvalidOperationException(
                $"Organization {organizationId} does not have Secrets Manager enabled.");
        }

        return organization;
    }
}
