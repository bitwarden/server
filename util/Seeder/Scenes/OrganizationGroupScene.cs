using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates an organization group, optionally adds organization users as members, and optionally grants the
/// group access to existing collections with per-assignment permissions.
/// </summary>
public class OrganizationGroupScene(
    IOrganizationRepository organizationRepository,
    IGroupRepository groupRepository,
    IManglerService manglerService) : IScene<OrganizationGroupScene.Request, OrganizationGroupScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string Name { get; set; }
        /// <summary>
        /// <c>OrganizationUser.Id</c> values (not <c>User.Id</c>) to add to the group as members after creation.
        /// </summary>
        public IEnumerable<Guid>? OrganizationUserIds { get; set; }
        public IEnumerable<AccessSelectionRequest>? Collections { get; set; }
    }

    /// <summary>
    /// A collection access grant for the new group. <see cref="Id"/> is the <c>Collection.Id</c> the group is granted access to.
    /// </summary>
    public class AccessSelectionRequest
    {
        [Required]
        public required Guid Id { get; set; }
        [Required]
        public bool ReadOnly { get; set; }
        [Required]
        public bool HidePasswords { get; set; }
        [Required]
        public bool Manage { get; set; }
    }

    public class Result
    {
        public required Guid GroupId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        var group = GroupSeeder.Create(organization.Id, request.Name);

        var collections = MapAccessSelections(request.Collections);

        await groupRepository.CreateAsync(group, collections ?? []);

        if (request.OrganizationUserIds?.Any() == true)
        {
            await groupRepository.AddGroupUsersByIdAsync(group.Id, request.OrganizationUserIds, DateTime.UtcNow);
        }

        return new SceneResult<Result>(
            result: new Result
            {
                GroupId = group.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }

    private static IEnumerable<CollectionAccessSelection>? MapAccessSelections(IEnumerable<AccessSelectionRequest>? selections)
        => selections?.Select(s => new CollectionAccessSelection
        {
            Id = s.Id,
            ReadOnly = s.ReadOnly,
            HidePasswords = s.HidePasswords,
            Manage = s.Manage
        });
}
