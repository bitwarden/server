#nullable enable
using Bit.Core.Models.Api;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;

namespace Bit.Api.NotificationCenter.Models.Response;

public class NotificationResponseModel : ResponseModel
{
    private const string _objectName = "notification";

    public NotificationResponseModel(Notification notification, string obj = _objectName)
        : base(obj)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        Id = notification.Id;
        Priority = notification.Priority;
        Title = notification.Title;
        Body = notification.Body;
        Date = notification.RevisionDate;
    }

    public NotificationResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }

    public Priority Priority { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public DateTime Date { get; set; }
}
