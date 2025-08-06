using System.Collections.Concurrent;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Identity.Test.Helpers;

public static class OrganizationAbilityBuilder
{
    public static ConcurrentDictionary<Guid, OrganizationAbility> BuildConcurrentDictionary(OrganizationUserOrganizationDetails orgUser, Organization organization) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, OrganizationAbility>(
                    orgUser.OrganizationId,
                    new OrganizationAbility(organization))
            });
}
