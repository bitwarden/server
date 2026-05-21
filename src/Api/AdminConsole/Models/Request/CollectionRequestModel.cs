// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Api.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request;

public class CreateCollectionRequestModel
{
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Name { get; set; }
    [StringLength(300)]
    public string ExternalId { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Groups { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Users { get; set; }
    public bool LeasingEnabled { get; set; }
    public object LeasingPolicy { get; set; }

    public Collection ToCollection(Guid orgId)
    {
        return ToCollection(new Collection
        {
            OrganizationId = orgId
        });
    }

    public virtual Collection ToCollection(Collection existingCollection)
    {
        existingCollection.Name = Name;
        existingCollection.ExternalId = ExternalId;
        existingCollection.LeasingEnabled = LeasingEnabled;
        existingCollection.LeasingPolicy = SerializeLeasingPolicy(LeasingPolicy);
        return existingCollection;
    }

    protected static string SerializeLeasingPolicy(object policy) => policy switch
    {
        null => null,
        JsonElement je when je.ValueKind == JsonValueKind.Null => null,
        JsonElement je => je.GetRawText(),
        _ => JsonSerializer.Serialize(policy),
    };
}

public class CollectionBulkDeleteRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}

public class CollectionWithIdRequestModel : CreateCollectionRequestModel
{
    public Guid? Id { get; set; }

    public override Collection ToCollection(Collection existingCollection)
    {
        existingCollection.Id = Id ?? Guid.Empty;
        return base.ToCollection(existingCollection);
    }
}

public class UpdateCollectionRequestModel : CreateCollectionRequestModel
{
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public new string Name { get; set; }

    public override Collection ToCollection(Collection existingCollection)
    {
        if (string.IsNullOrEmpty(existingCollection.DefaultUserCollectionEmail) && !string.IsNullOrWhiteSpace(Name))
        {
            existingCollection.Name = Name;
        }
        existingCollection.ExternalId = ExternalId;
        existingCollection.LeasingEnabled = LeasingEnabled;
        existingCollection.LeasingPolicy = SerializeLeasingPolicy(LeasingPolicy);
        return existingCollection;
    }

}
