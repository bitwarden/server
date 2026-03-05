using System.Text.Json;
using Bit.Api.Tools.Models;
using Bit.Api.Tools.Models.Request;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
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
            Text = new SendTextModel()
            {
                Hidden = false,
                Text = "encrypted_text"
            },
            Type = SendType.Text,
        };

        var sendAuthorizationService = Substitute.For<ISendAuthorizationService>();
        sendAuthorizationService.HashPassword(Arg.Any<string>())
            .Returns((info) => $"hashed_{(string)info[0]}");

        var send = sendRequest.ToSend(Guid.NewGuid(), sendAuthorizationService);

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

    // UpdateSend auth preservation tests

    private static Send ExistingSendWithPassword() => new Send
    {
        Id = Guid.NewGuid(),
        Type = SendType.Text,
        Password = "hashed_existing_password",
        Emails = null,
        AuthType = AuthType.Password,
        Key = "key",
        DeletionDate = DateTime.UtcNow.AddDays(7),
    };

    private static Send ExistingSendWithEmails() => new Send
    {
        Id = Guid.NewGuid(),
        Type = SendType.Text,
        Password = null,
        Emails = "user@example.com",
        AuthType = AuthType.Email,
        Key = "key",
        DeletionDate = DateTime.UtcNow.AddDays(7),
    };

    private static SendRequestModel MinimalTextUpdateRequest() => new SendRequestModel
    {
        Type = SendType.Text,
        Name = "updated_name",
        Text = new SendTextModel { Text = "text", Hidden = false },
        Key = "key",
        DeletionDate = DateTime.UtcNow.AddDays(7),
        Disabled = false,
    };

    [Fact]
    public void UpdateSend_OmitsPassword_PreservesExistingPasswordProtection()
    {
        var existingSend = ExistingSendWithPassword();
        var request = MinimalTextUpdateRequest(); // no Password field
        var authService = Substitute.For<ISendAuthorizationService>();

        var updated = request.UpdateSend(existingSend, authService);

        Assert.Equal("hashed_existing_password", updated.Password);
        Assert.Equal(AuthType.Password, updated.AuthType);
        Assert.Null(updated.Emails);
    }

    [Fact]
    public void UpdateSend_OmitsEmails_PreservesExistingEmailProtection()
    {
        var existingSend = ExistingSendWithEmails();
        var request = MinimalTextUpdateRequest(); // no Emails field
        var authService = Substitute.For<ISendAuthorizationService>();

        var updated = request.UpdateSend(existingSend, authService);

        Assert.Equal("user@example.com", updated.Emails);
        Assert.Equal(AuthType.Email, updated.AuthType);
        Assert.Null(updated.Password);
    }

    [Fact]
    public void UpdateSend_ProvidesNewPassword_ReplacesExistingPasswordProtection()
    {
        var existingSend = ExistingSendWithPassword();
        var request = MinimalTextUpdateRequest();
        request.Password = "new_password";
        var authService = Substitute.For<ISendAuthorizationService>();
        authService.HashPassword("new_password").Returns("hashed_new_password");

        var updated = request.UpdateSend(existingSend, authService);

        Assert.Equal("hashed_new_password", updated.Password);
        Assert.Equal(AuthType.Password, updated.AuthType);
        Assert.Null(updated.Emails);
    }

    [Fact]
    public void UpdateSend_ProvidesEmails_ReplacesExistingPasswordProtection()
    {
        var existingSend = ExistingSendWithPassword();
        var request = MinimalTextUpdateRequest();
        request.Emails = "new@example.com";
        var authService = Substitute.For<ISendAuthorizationService>();

        var updated = request.UpdateSend(existingSend, authService);

        Assert.Equal("new@example.com", updated.Emails);
        Assert.Equal(AuthType.Email, updated.AuthType);
        Assert.Null(updated.Password);
    }

    [Fact]
    public void UpdateSend_ProvidesPassword_ReplacesExistingEmailProtection()
    {
        var existingSend = ExistingSendWithEmails();
        var request = MinimalTextUpdateRequest();
        request.Password = "new_password";
        var authService = Substitute.For<ISendAuthorizationService>();
        authService.HashPassword("new_password").Returns("hashed_new_password");

        var updated = request.UpdateSend(existingSend, authService);

        Assert.Equal("hashed_new_password", updated.Password);
        Assert.Equal(AuthType.Password, updated.AuthType);
        Assert.Null(updated.Emails);
    }

    [Fact]
    public void ToSend_WithoutAuth_SetsAuthTypeNone()
    {
        var request = MinimalTextUpdateRequest(); // no Password or Emails
        var authService = Substitute.For<ISendAuthorizationService>();

        var send = request.ToSend(Guid.NewGuid(), authService);

        Assert.Equal(AuthType.None, send.AuthType);
        Assert.Null(send.Password);
        Assert.Null(send.Emails);
    }

    [Fact]
    public void ValidateEdit_DeletionDateInPast_ThrowsBadRequestException()
    {
        var send = new SendRequestModel
        {
            DeletionDate = DateTime.UtcNow.AddMinutes(-5)
        };

        Assert.Throws<BadRequestException>(() => send.ValidateEdit());
    }

    [Fact]
    public void ValidateEdit_DeletionDateTooFarInFuture_ThrowsBadRequestException()
    {
        var send = new SendRequestModel
        {
            DeletionDate = DateTime.UtcNow.AddDays(32)
        };

        Assert.Throws<BadRequestException>(() => send.ValidateEdit());
    }

    [Fact]
    public void ValidateEdit_ExpirationDateInPast_ThrowsBadRequestException()
    {
        var send = new SendRequestModel
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-5)
        };

        Assert.Throws<BadRequestException>(() => send.ValidateEdit());
    }

    [Fact]
    public void ValidateEdit_ExpirationDateGreaterThanDeletionDate_ThrowsBadRequestException()
    {
        var send = new SendRequestModel
        {
            DeletionDate = DateTime.UtcNow.AddDays(1),
            ExpirationDate = DateTime.UtcNow.AddDays(2)
        };

        Assert.Throws<BadRequestException>(() => send.ValidateEdit());
    }

    [Fact]
    public void ValidateEdit_ValidDates_Success()
    {
        var send = new SendRequestModel
        {
            DeletionDate = DateTime.UtcNow.AddDays(10),
            ExpirationDate = DateTime.UtcNow.AddDays(5)
        };

        Exception ex = Record.Exception(() => send.ValidateEdit());

        Assert.Null(ex);
    }
}
