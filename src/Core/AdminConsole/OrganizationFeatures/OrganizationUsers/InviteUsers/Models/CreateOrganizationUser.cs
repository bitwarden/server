using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class CreateOrganizationUser
{
    public OrganizationUser OrganizationUser { get; set; }
    public CollectionAccessSelection[] Collections { get; set; } = [];
    public Guid[] Groups { get; set; } = [];

    public static Func<OrganizationUserInviteDto, CreateOrganizationUser> MapToDataModel(DateTimeOffset performedAt) =>
        o => new CreateOrganizationUser
        {
            OrganizationUser = new OrganizationUser
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = o.OrganizationId,
                Email = o.Email.ToLowerInvariant(),
                Type = o.Type,
                Status = OrganizationUserStatusType.Invited,
                AccessSecretsManager = o.AccessSecretsManager,
                ExternalId = string.IsNullOrWhiteSpace(o.ExternalId) ? null : o.ExternalId,
                CreationDate = performedAt.UtcDateTime,
                RevisionDate = performedAt.UtcDateTime
            },
            Collections = o.AssignedCollections,
            Groups = o.Groups
        };
}
