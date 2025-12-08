using System.Text.Json.Serialization;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class EventItem : IEvent
{
    public EventItem() {}

    public EventItem(IEvent e)
    {
        Id = Guid.NewGuid().ToString();
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

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public EventType Type { get; set; }
    [JsonPropertyName("ip")]
    public string IpAddress { get; set; }
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    [JsonPropertyName("device")]
    public DeviceType? DeviceType { get; set; }
    [JsonPropertyName("sUser")]
    public EventSystemUser? SystemUser { get; set; }
    [JsonPropertyName("uId")]
    public Guid? UserId { get; set; }
    [JsonPropertyName("oId")]
    public Guid? OrganizationId { get; set; }
    [JsonPropertyName("inId")]
    public Guid? InstallationId { get; set; }
    [JsonPropertyName("prId")]
    public Guid? ProviderId { get; set; }
    [JsonPropertyName("cipId")]
    public Guid? CipherId { get; set; }
    [JsonPropertyName("colId")]
    public Guid? CollectionId { get; set; }
    [JsonPropertyName("grpId")]
    public Guid? GroupId { get; set; }
    [JsonPropertyName("polId")]
    public Guid? PolicyId { get; set; }
    [JsonPropertyName("ouId")]
    public Guid? OrganizationUserId { get; set; }
    [JsonPropertyName("pruId")]
    public Guid? ProviderUserId { get; set; }
    [JsonPropertyName("proId")]
    public Guid? ProviderOrganizationId { get; set; }
    [JsonPropertyName("auId")]
    public Guid? ActingUserId { get; set; }
    [JsonPropertyName("secId")]
    public Guid? SecretId { get; set; }
    [JsonPropertyName("saId")]
    public Guid? ServiceAccountId { get; set; }
    [JsonPropertyName("domain")]
    public string DomainName { get; set; }
}
