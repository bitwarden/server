using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Public;

public abstract class CollectionBaseModel
{
    /// <summary>
    /// External identifier for reference or linking this collection to another system.
    /// </summary>
    /// <example>external_id_123456</example>
    [StringLength(300)]
    public string ExternalId { get; set; }
}
