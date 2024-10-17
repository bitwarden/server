#nullable enable
using Bit.Api.NotificationCenter.Models.Response;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Models.Data;
using Xunit;

namespace Bit.Api.Test.NotificationCenter.Models.Response;

public class NotificationResponseModelTests
{
    [Fact]
    public void Constructor_NotificationStatusDetailsNull_CorrectFields()
    {
        Assert.Throws<ArgumentNullException>(() => new NotificationResponseModel(null!));
    }

    [Fact]
    public void Constructor_NotificationStatusDetails_CorrectFields()
    {
        var notificationStatusDetails = new NotificationStatusDetails
        {
            Id = Guid.NewGuid(),
            Global = true,
            Priority = Priority.High,
            ClientType = ClientType.All,
            Title = "Test Title",
            Body = "Test Body",
            RevisionDate = DateTime.UtcNow - TimeSpan.FromMinutes(3),
            ReadDate = DateTime.UtcNow - TimeSpan.FromMinutes(1),
            DeletedDate = DateTime.UtcNow,
        };
        var model = new NotificationResponseModel(notificationStatusDetails);

        Assert.Equal(model.Id, notificationStatusDetails.Id);
        Assert.Equal(model.Priority, notificationStatusDetails.Priority);
        Assert.Equal(model.Title, notificationStatusDetails.Title);
        Assert.Equal(model.Body, notificationStatusDetails.Body);
        Assert.Equal(model.Date, notificationStatusDetails.RevisionDate);
        Assert.Equal(model.ReadDate, notificationStatusDetails.ReadDate);
        Assert.Equal(model.DeletedDate, notificationStatusDetails.DeletedDate);
    }
}
