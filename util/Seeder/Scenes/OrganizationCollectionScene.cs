using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates an organization collection (name encrypted with the organization's symmetric key) and
/// optionally assigns organization users and/or groups to it with per-assignment permissions.
/// </summary>
public class OrganizationCollectionScene(
    IOrganizationRepository organizationRepository,
    ICollectionRepository collectionRepository,
    IOrganizationUserRepository organizationUserRepository,
    IGroupRepository groupRepository,
    IManglerService manglerService) : IScene<OrganizationCollectionScene.Request, OrganizationCollectionScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string OrganizationKeyB64 { get; set; }
        [Required]
        public required string Name { get; set; }
        public IEnumerable<AccessSelectionRequest>? Users { get; set; }
        public IEnumerable<AccessSelectionRequest>? Groups { get; set; }
    }

    /// <summary>
    /// A collection access assignment. For the <see cref="Request.Users"/> list, <see cref="Id"/> is the
    /// <c>OrganizationUser.Id</c> (not the User's Id); for <see cref="Request.Groups"/>, it is the <c>Group.Id</c>.
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
        public required Guid CollectionId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        await organizationUserRepository.ThrowIfOrganizationUsersNotInOrganizationAsync(
            request.Users?.Select(u => u.Id), organization.Id);
        await groupRepository.ThrowIfGroupsNotInOrganizationAsync(request.Groups?.Select(g => g.Id), organization.Id);

        var users = MapAccessSelections(request.Users).DistinctOrThrowIfConflicting("organization user");
        var groups = MapAccessSelections(request.Groups).DistinctOrThrowIfConflicting("group");

        var collection = CollectionSeeder.Create(organization.Id, request.OrganizationKeyB64, request.Name);

        await collectionRepository.CreateAsync(collection, groups: groups, users: users);

        return new SceneResult<Result>(
            result: new Result
            {
                CollectionId = collection.Id
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
