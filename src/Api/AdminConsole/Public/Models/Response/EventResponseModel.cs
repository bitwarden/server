using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Public.Response;

/// <summary>
/// An event log.
/// </summary>
public class EventResponseModel : IResponseModel
{
    public EventResponseModel(IEvent ev)
    {
        if (ev == null)
        {
            throw new ArgumentNullException(nameof(ev));
        }

        Type = ev.Type;
        ItemId = ev.CipherId;
        CollectionId = ev.CollectionId;
        GroupId = ev.GroupId;
        PolicyId = ev.PolicyId;
        MemberId = ev.OrganizationUserId;
        ActingUserId = ev.ActingUserId;
        Date = ev.Date;
        Device = ev.DeviceType;
        IpAddress = ev.IpAddress;
        InstallationId = ev.InstallationId;
    }

    /// <summary>
    /// String representing the object's type. Objects of the same type share the same properties.
    /// </summary>
    /// <example>event</example>
    [Required]
    public string Object => "event";
    /// <summary>
    /// The type of event.
    /// </summary>
    [Required]
    public EventType Type { get; set; }
    /// <summary>
    /// The unique identifier of the related item that the event describes.
    /// </summary>
    /// <example>3767a302-8208-4dc6-b842-030428a1cfad</example>
    public Guid? ItemId { get; set; }
    /// <summary>
    /// The unique identifier of the related collection that the event describes.
    /// </summary>
    /// <example>bce212a4-25f3-4888-8a0a-4c5736d851e0</example>
    public Guid? CollectionId { get; set; }
    /// <summary>
    /// The unique identifier of the related group that the event describes.
    /// </summary>
    /// <example>f29a2515-91d2-4452-b49b-5e8040e6b0f4</example>
    public Guid? GroupId { get; set; }
    /// <summary>
    /// The unique identifier of the related policy that the event describes.
    /// </summary>
    /// <example>f29a2515-91d2-4452-b49b-5e8040e6b0f4</example>
    public Guid? PolicyId { get; set; }
    /// <summary>
    /// The unique identifier of the related member that the event describes.
    /// </summary>
    /// <example>e68b8629-85eb-4929-92c0-b84464976ba4</example>
    public Guid? MemberId { get; set; }
    /// <summary>
    /// The unique identifier of the user that performed the event.
    /// </summary>
    /// <example>a2549f79-a71f-4eb9-9234-eb7247333f94</example>
    public Guid? ActingUserId { get; set; }
    /// <summary>
    /// The Unique identifier of the Installation that performed the event.
    /// </summary>
    /// <value></value>
    public Guid? InstallationId { get; set; }
    /// <summary>
    /// The date/timestamp when the event occurred.
    /// </summary>
    [Required]
    public DateTime Date { get; set; }
    /// <summary>
    /// The type of device used by the acting user when the event occurred.
    /// </summary>
    public DeviceType? Device { get; set; }
    /// <summary>
    /// The IP address of the acting user.
    /// </summary>
    /// <example>172.16.254.1</example>
    public string IpAddress { get; set; }
}
