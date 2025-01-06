﻿using Azure;
using Azure.Data.Tables;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data;

// used solely for interaction with Azure Table Storage
public class AzureEvent : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public DateTime Date { get; set; }
    public int Type { get; set; }
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
    public int? DeviceType { get; set; }
    public string IpAddress { get; set; }
    public Guid? ActingUserId { get; set; }
    public int? SystemUser { get; set; }
    public string DomainName { get; set; }
    public Guid? SecretId { get; set; }
    public Guid? ServiceAccountId { get; set; }

    public EventTableEntity ToEventTableEntity()
    {
        return new EventTableEntity
        {
            PartitionKey = PartitionKey,
            RowKey = RowKey,
            Timestamp = Timestamp,
            ETag = ETag,

            Date = Date,
            Type = (EventType)Type,
            UserId = UserId,
            OrganizationId = OrganizationId,
            InstallationId = InstallationId,
            ProviderId = ProviderId,
            CipherId = CipherId,
            CollectionId = CollectionId,
            PolicyId = PolicyId,
            GroupId = GroupId,
            OrganizationUserId = OrganizationUserId,
            ProviderUserId = ProviderUserId,
            ProviderOrganizationId = ProviderOrganizationId,
            DeviceType = DeviceType.HasValue ? (DeviceType)DeviceType.Value : null,
            IpAddress = IpAddress,
            ActingUserId = ActingUserId,
            SystemUser = SystemUser.HasValue ? (EventSystemUser)SystemUser.Value : null,
            DomainName = DomainName,
            SecretId = SecretId,
            ServiceAccountId = ServiceAccountId
        };
    }
}

public class EventTableEntity : IEvent
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
        DomainName = e.DomainName;
        SecretId = e.SecretId;
        ServiceAccountId = e.ServiceAccountId;
    }

    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

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
    public string DomainName { get; set; }
    public Guid? SecretId { get; set; }
    public Guid? ServiceAccountId { get; set; }

    public AzureEvent ToAzureEvent()
    {
        return new AzureEvent
        {
            PartitionKey = PartitionKey,
            RowKey = RowKey,
            Timestamp = Timestamp,
            ETag = ETag,

            Date = Date,
            Type = (int)Type,
            UserId = UserId,
            OrganizationId = OrganizationId,
            InstallationId = InstallationId,
            ProviderId = ProviderId,
            CipherId = CipherId,
            CollectionId = CollectionId,
            PolicyId = PolicyId,
            GroupId = GroupId,
            OrganizationUserId = OrganizationUserId,
            ProviderUserId = ProviderUserId,
            ProviderOrganizationId = ProviderOrganizationId,
            DeviceType = DeviceType.HasValue ? (int)DeviceType.Value : null,
            IpAddress = IpAddress,
            ActingUserId = ActingUserId,
            SystemUser = SystemUser.HasValue ? (int)SystemUser.Value : null,
            DomainName = DomainName,
            SecretId = SecretId,
            ServiceAccountId = ServiceAccountId
        };
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

        if (e.OrganizationId.HasValue && e.ServiceAccountId.HasValue)
        {
            entities.Add(new EventTableEntity(e)
            {
                PartitionKey = pKey,
                RowKey = $"ServiceAccountId={e.ServiceAccountId}__Date={dateKey}__Uniquifier={uniquifier}"
            });
        }

        if (e.SecretId.HasValue)
        {
            entities.Add(new EventTableEntity(e)
            {
                PartitionKey = pKey,
                RowKey = $"SecretId={e.SecretId}__Date={dateKey}__Uniquifier={uniquifier}"
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
