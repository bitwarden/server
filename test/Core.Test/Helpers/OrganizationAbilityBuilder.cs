using System.Collections.Concurrent;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Test.Helpers;

public static class OrganizationAbilityBuilder
{
    public static ConcurrentDictionary<Guid, OrganizationAbility> BuildConcurrentDictionary(Guid organizationId) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, OrganizationAbility>(
                    organizationId,
                    new OrganizationAbility
                    {
                        Id = organizationId,
                        LimitItemDeletion = true
                    })
            });

    public static ConcurrentDictionary<Guid, OrganizationAbility> BuildConcurrentDictionary(Organization organization) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, OrganizationAbility>(
                    organization.Id,
                    new OrganizationAbility(organization))
            });

    public static ConcurrentDictionary<Guid, OrganizationAbility> BuildConcurrentDictionary(OrganizationUser orgUser) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, OrganizationAbility>(
                    orgUser.OrganizationId,
                    new OrganizationAbility
                    {
                        Id = orgUser.OrganizationId,
                        UseEvents = true,
                        Enabled = true
                    })
            });

    public static ConcurrentDictionary<Guid, OrganizationAbility> BuildConcurrentDictionary(Group group) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, OrganizationAbility>(
                    group.OrganizationId,
                    new OrganizationAbility
                    {
                        Id = group.OrganizationId,
                        UseEvents = true,
                        Enabled = true
                    })
            });

    public static ConcurrentDictionary<Guid, OrganizationAbility> BuildConcurrentDictionary(OrganizationUserOrganizationDetails orgUser, Organization organization) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, OrganizationAbility>(
                    orgUser.OrganizationId,
                    new OrganizationAbility(organization))
            });

    public static ConcurrentDictionary<Guid, OrganizationAbility> BuildConcurrentDictionary(CipherDetails cipherDetails) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, OrganizationAbility>(
                    cipherDetails.OrganizationId!.Value,
                    new OrganizationAbility())
            });
    public static ConcurrentDictionary<Guid, OrganizationAbility> BuildConcurrentDictionary(ICollection<Collection> collections) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, OrganizationAbility>(
                    collections.First().OrganizationId,
                    new OrganizationAbility
                    {
                        LimitCollectionCreation = true,
                        LimitCollectionDeletion = true,
                        AllowAdminAccessToAllCollectionItems = true
                    })
            });

}
