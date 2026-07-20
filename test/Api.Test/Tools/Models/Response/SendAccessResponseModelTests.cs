using System.Text.Json;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Xunit;

namespace Bit.Api.Test.Models.Response;

public class SendAccessResponseModelTests
{
    [Fact]
    public void NullSend_Throws()
    {
      Send send = null;
      SendAccessResponseModel responseModel;
      Assert.Throws<ArgumentNullException>(() => responseModel = new SendAccessResponseModel(send));
    }

    [Fact]
    public void TextSend_NullData_Throws()
    {
      var send = new Send
      {
        Type = SendType.Text,
        Data = null
      };
      SendAccessResponseModel responseModel;
      Assert.Throws<NullReferenceException>(() => responseModel = new SendAccessResponseModel(send));
    }

    [Fact]
    public void TextSend_NonDeserializableData_Throws()
    {
      var send = new Send
      {
        Type = SendType.Text,
        Data = "bad_data"
      };
      SendAccessResponseModel responseModel;
      Assert.Throws<JsonException>(() => responseModel = new SendAccessResponseModel(send));
    }

    [Fact]
    public void FileSend_NullData_Throws()
    {
      var send = new Send
      {
        Type = SendType.File,
        Data = null
      };
      SendAccessResponseModel responseModel;
      Assert.Throws<NullReferenceException>(() => responseModel = new SendAccessResponseModel(send));
    }

    [Fact]
    public void FileSend_NonDeserializableData_Throws()
    {
      var send = new Send
      {
        Type = SendType.File,
        Data = "bad_data"
      };
      SendAccessResponseModel responseModel;
      Assert.Throws<JsonException>(() => responseModel = new SendAccessResponseModel(send));
    }

    [Fact]
    public void ItemSend_NullData_Throws()
    {
      var send = new Send
      {
        Type = SendType.Item,
        Data = null
      };
      SendAccessResponseModel responseModel;
      Assert.Throws<NullReferenceException>(() => responseModel = new SendAccessResponseModel(send));
    }

    [Fact]
    public void FromSend_Success()
    {
      var send = new Send
      {
        Id = Guid.NewGuid(),
        AuthType = AuthType.None,
        Key = "encrypted_key",
        MaxAccessCount = 5,
        AccessCount = 0,
        RevisionDate = new DateTime(),
        ExpirationDate = new DateTime(),
        DeletionDate = new DateTime(),
        Password = null,
        Emails = null,
        Disabled = false,
        HideEmail = false,
        Type = SendType.Text,
        Data = JsonSerializer.Serialize(new SendTextData
        {
          Hidden = false,
          Name = "encrypted_name",
          Notes = null,
          Text = "encrypted_text"
        })
      };
      var responseModel = new SendAccessResponseModel(send);
      Assert.Equal(send.Type, responseModel.Type);
      Assert.Equal(send.AuthType, responseModel.AuthType);
      Assert.Equal("encrypted_name", responseModel.Name);
      Assert.Equal("encrypted_text", responseModel.Text.Text);
      Assert.False(responseModel.Text.Hidden);
      Assert.Equal(send.ExpirationDate, responseModel.ExpirationDate);
    }
}