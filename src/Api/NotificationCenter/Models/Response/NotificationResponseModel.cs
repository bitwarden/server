#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Models.Data;

namespace Bit.Api.NotificationCenter.Models.Response;

public class NotificationResponseModel : ResponseModel
{
    private const string _objectName = "notification";

    public NotificationResponseModel(NotificationStatusDetails notificationStatusDetails, string obj = _objectName)
        : base(obj)
    {
        if (notificationStatusDetails == null)
        {
            throw new ArgumentNullException(nameof(notificationStatusDetails));
        }

        Id = notificationStatusDetails.Id;
        Priority = notificationStatusDetails.Priority;
        Title = notificationStatusDetails.Title;
        Body = notificationStatusDetails.Body;
        Date = notificationStatusDetails.RevisionDate;
        ReadDate = notificationStatusDetails.ReadDate;
        DeletedDate = notificationStatusDetails.DeletedDate;
    }

    public NotificationResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }

    public Priority Priority { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public DateTime Date { get; set; }

    public DateTime? ReadDate { get; set; }

    public DateTime? DeletedDate { get; set; }
}
