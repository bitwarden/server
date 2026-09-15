using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections;

public class CollectionAccessValidator(
    IGroupRepository groupRepository,
    IOrganizationUserRepository organizationUserRepository)
    : ICollectionAccessValidator
{
    public async Task<ValidationResult<CollectionAccessValidationRequest>> ValidateAsync(
        CollectionAccessValidationRequest request)
    {
        if (request.Groups is { Count: > 0 })
        {
            var groupIds = request.Groups.Select(g => g.Id).Distinct().ToList();
            var groups = await groupRepository.GetManyByManyIds(groupIds);

            if (groups.Count != groupIds.Count ||
                groups.Any(g => g.OrganizationId != request.OrganizationId))
            {
                return Invalid(request, new CollectionAccessInvalidError());
            }
        }

        if (request.Users is { Count: > 0 })
        {
            var userIds = request.Users.Select(u => u.Id).Distinct().ToList();
            var users = await organizationUserRepository.GetManyAsync(userIds);

            if (users.Count != userIds.Count ||
                users.Any(u => u.OrganizationId != request.OrganizationId))
            {
                return Invalid(request, new CollectionAccessInvalidError());
            }
        }

        return Valid(request);
    }
}
