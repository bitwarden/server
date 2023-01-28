using Bit.Core.Enums;
using Bit.Core.Utilities;
using Microsoft.Azure.Cosmos.Table;

namespace Bit.Core.Models.Data;

public class EventTableEntity : TableEntity, IEvent
{
    public EventTableEntity() { }

    private EventTableEntity(IEvent e)
    {
        Date = e.Date;
        Type = e.Type;
        UserId = e.UserId;
        OrganizationId = e.OrganizationId;
        InstallationId = e.InstallationId;
        ProviderId = e.ProviderId;
        CipherId = e.CipherId;
        CollectionId = e.CollectionId;
        PolicyId = e.PolicyId;
        GroupId = e.GroupId;
        OrganizationUserId = e.OrganizationUserId;
        ProviderUserId = e.ProviderUserId;
        ProviderOrganizationId = e.ProviderOrganizationId;
        DeviceType = e.DeviceType;
        IpAddress = e.IpAddress;
        ActingUserId = e.ActingUserId;
        SystemUser = e.SystemUser;
    }

    public DateTime Date { get; set; }
    public EventType Type { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? InstallationId { get; set; }
    public Guid? ProviderId { get; set; }
    public Guid? CipherId { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? OrganizationUserId { get; set; }
    public Guid? ProviderUserId { get; set; }
    public Guid? ProviderOrganizationId { get; set; }
    public DeviceType? DeviceType { get; set; }
    public string IpAddress { get; set; }
    public Guid? ActingUserId { get; set; }
    public EventSystemUser? SystemUser { get; set; }

    public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
    {
        var result = base.WriteEntity(operationContext);

        var typeName = nameof(Type);
        if (result.ContainsKey(typeName))
        {
            result[typeName] = new EntityProperty((int)Type);
        }
        else
        {
            result.Add(typeName, new EntityProperty((int)Type));
        }

        var deviceTypeName = nameof(DeviceType);
        if (result.ContainsKey(deviceTypeName))
        {
            result[deviceTypeName] = new EntityProperty((int?)DeviceType);
        }
        else
        {
            result.Add(deviceTypeName, new EntityProperty((int?)DeviceType));
        }

        var systemUserTypeName = nameof(SystemUser);
        if (result.ContainsKey(systemUserTypeName))
        {
            result[systemUserTypeName] = new EntityProperty((int?)SystemUser);
        }
        else
        {
            result.Add(systemUserTypeName, new EntityProperty((int?)SystemUser));
        }

        return result;
    }

    public override void ReadEntity(IDictionary<string, EntityProperty> properties,
        OperationContext operationContext)
    {
        base.ReadEntity(properties, operationContext);

        var typeName = nameof(Type);
        if (properties.ContainsKey(typeName) && properties[typeName].Int32Value.HasValue)
        {
            Type = (EventType)properties[typeName].Int32Value.Value;
        }

        var deviceTypeName = nameof(DeviceType);
        if (properties.ContainsKey(deviceTypeName) && properties[deviceTypeName].Int32Value.HasValue)
        {
            DeviceType = (DeviceType)properties[deviceTypeName].Int32Value.Value;
        }

        var systemUserTypeName = nameof(SystemUser);
        if (properties.ContainsKey(systemUserTypeName) && properties[systemUserTypeName].Int32Value.HasValue)
        {
            SystemUser = (EventSystemUser)properties[systemUserTypeName].Int32Value.Value;
        }
    }

    public static List<EventTableEntity> IndexEvent(EventMessage e)
    {
        var uniquifier = e.IdempotencyId.GetValueOrDefault(Guid.NewGuid());

        var pKey = GetPartitionKey(e);

        var dateKey = CoreHelpers.DateTimeToTableStorageKey(e.Date);

        var entities = new List<EventTableEntity>
        {
            new EventTableEntity(e)
            {
                PartitionKey = pKey,
                RowKey = $"Date={dateKey}__Uniquifier={uniquifier}"
            }
        };

        if (e.OrganizationId.HasValue && e.ActingUserId.HasValue)
        {
            entities.Add(new EventTableEntity(e)
            {
                PartitionKey = pKey,
                RowKey = $"ActingUserId={e.ActingUserId}__Date={dateKey}__Uniquifier={uniquifier}"
            });
        }

        if (!e.OrganizationId.HasValue && e.ProviderId.HasValue && e.ActingUserId.HasValue)
        {
            entities.Add(new EventTableEntity(e)
            {
                PartitionKey = pKey,
                RowKey = $"ActingUserId={e.ActingUserId}__Date={dateKey}__Uniquifier={uniquifier}"
            });
        }

        if (e.CipherId.HasValue)
        {
            entities.Add(new EventTableEntity(e)
            {
                PartitionKey = pKey,
                RowKey = $"CipherId={e.CipherId}__Date={dateKey}__Uniquifier={uniquifier}"
            });
        }

        return entities;
    }

    private static string GetPartitionKey(EventMessage e)
    {
        if (e.OrganizationId.HasValue)
        {
            return $"OrganizationId={e.OrganizationId}";
        }

        if (e.ProviderId.HasValue)
        {
            return $"ProviderId={e.ProviderId}";
        }

        return $"UserId={e.UserId}";
    }
}
