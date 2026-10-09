using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct OrganizationDomainSceneResult
{
    public Guid OrganizationDomainId { get; init; }
}

/// <summary>
/// Adds a verified claimed domain to an existing organization. The domain is stamped as verified
/// without a DNS TXT check, so seeded members whose email falls under it count as claimed.
/// </summary>
public class OrganizationDomainScene(
    IOrganizationRepository organizationRepository,
    IOrganizationDomainRepository organizationDomainRepository,
    IManglerService manglerService) : IScene<OrganizationDomainScene.Request, OrganizationDomainSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string DomainName { get; set; }
    }

    public async Task<SceneResult<OrganizationDomainSceneResult>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        var domain = OrganizationDomainSeeder.Create(request.OrganizationId, request.DomainName);
        await organizationDomainRepository.CreateAsync(domain);

        return new SceneResult<OrganizationDomainSceneResult>(
            result: new OrganizationDomainSceneResult
            {
                OrganizationDomainId = domain.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
