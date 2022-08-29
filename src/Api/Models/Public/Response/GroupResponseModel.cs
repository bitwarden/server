using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Public.Response;

/// <summary>
/// A user group.
/// </summary>
public class GroupResponseModel : GroupBaseModel, IResponseModel
{
    public GroupResponseModel(Group group, IEnumerable<SelectionReadOnly> collections)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        Id = group.Id;
        Name = group.Name;
        AccessAll = group.AccessAll;
        ExternalId = group.ExternalId;
        Collections = collections?.Select(c => new AssociationWithPermissionsResponseModel(c));
    }

    /// <summary>
    /// String representing the object's type. Objects of the same type share the same properties.
    /// </summary>
    /// <example>group</example>
    [Required]
    public string Object => "group";
    /// <summary>
    /// The group's unique identifier.
    /// </summary>
    /// <example>539a36c5-e0d2-4cf9-979e-51ecf5cf6593</example>
    [Required]
    public Guid Id { get; set; }
    /// <summary>
    /// The associated collections that this group can access.
    /// </summary>
    public IEnumerable<AssociationWithPermissionsResponseModel> Collections { get; set; }
}
