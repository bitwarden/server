using System.Text.Json;
using Bit.Api.Tools.Models;
using Bit.Api.Tools.Models.Request;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Services;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Models.Request;

public class SendRequestModelTests
{
    [Fact]
    public void ToSend_Text_Success()
    {
        var deletionDate = DateTime.UtcNow.AddDays(5);
        var sendRequest = new SendRequestModel
        {
            DeletionDate = deletionDate,
            Disabled = false,
            ExpirationDate = null,
            HideEmail = false,
            Key = "encrypted_key",
            MaxAccessCount = null,
            Name = "encrypted_name",
            Notes = null,
            Password = "Password",
            Text = new SendTextModel() { Hidden = false, Text = "encrypted_text" },
            Type = SendType.Text,
        };

        var sendService = Substitute.For<ISendService>();
        sendService.HashPassword(Arg.Any<string>()).Returns((info) => $"hashed_{(string)info[0]}");

        var send = sendRequest.ToSend(Guid.NewGuid(), sendService);

        Assert.Equal(deletionDate, send.DeletionDate);
        Assert.False(send.Disabled);
        Assert.Null(send.ExpirationDate);
        Assert.False(send.HideEmail);
        Assert.Equal("encrypted_key", send.Key);
        Assert.Equal("hashed_Password", send.Password);

        using var jsonDocument = JsonDocument.Parse(send.Data);
        var root = jsonDocument.RootElement;
        var text = AssertHelper.AssertJsonProperty(root, "Text", JsonValueKind.String).GetString();
        Assert.Equal("encrypted_text", text);
        AssertHelper.AssertJsonProperty(root, "Hidden", JsonValueKind.False);
        Assert.False(root.TryGetProperty("Notes", out var _));
        var name = AssertHelper.AssertJsonProperty(root, "Name", JsonValueKind.String).GetString();
        Assert.Equal("encrypted_name", name);
    }

    [Fact]
    public void ValidateEdit_DeletionDateInPast_ThrowsBadRequestException()
    {
        var send = new SendRequestModel { DeletionDate = DateTime.UtcNow.AddMinutes(-5) };

        Assert.Throws<BadRequestException>(() => send.ValidateEdit());
    }

    [Fact]
    public void ValidateEdit_DeletionDateTooFarInFuture_ThrowsBadRequestException()
    {
        var send = new SendRequestModel { DeletionDate = DateTime.UtcNow.AddDays(32) };

        Assert.Throws<BadRequestException>(() => send.ValidateEdit());
    }

    [Fact]
    public void ValidateEdit_ExpirationDateInPast_ThrowsBadRequestException()
    {
        var send = new SendRequestModel { ExpirationDate = DateTime.UtcNow.AddMinutes(-5) };

        Assert.Throws<BadRequestException>(() => send.ValidateEdit());
    }

    [Fact]
    public void ValidateEdit_ExpirationDateGreaterThanDeletionDate_ThrowsBadRequestException()
    {
        var send = new SendRequestModel
        {
            DeletionDate = DateTime.UtcNow.AddDays(1),
            ExpirationDate = DateTime.UtcNow.AddDays(2),
        };

        Assert.Throws<BadRequestException>(() => send.ValidateEdit());
    }

    [Fact]
    public void ValidateEdit_ValidDates_Success()
    {
        var send = new SendRequestModel
        {
            DeletionDate = DateTime.UtcNow.AddDays(10),
            ExpirationDate = DateTime.UtcNow.AddDays(5),
        };

        Exception ex = Record.Exception(() => send.ValidateEdit());

        Assert.Null(ex);
    }
}
