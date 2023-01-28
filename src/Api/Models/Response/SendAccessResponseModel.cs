using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response;

public class SendAccessResponseModel : ResponseModel
{
    public SendAccessResponseModel(Send send, GlobalSettings globalSettings)
        : base("send-access")
    {
        if (send == null)
        {
            throw new ArgumentNullException(nameof(send));
        }

        Id = CoreHelpers.Base64UrlEncode(send.Id.ToByteArray());
        Type = send.Type;

        SendData sendData;
        switch (send.Type)
        {
            case SendType.File:
                var fileData = JsonSerializer.Deserialize<SendFileData>(send.Data);
                sendData = fileData;
                File = new SendFileModel(fileData);
                break;
            case SendType.Text:
                var textData = JsonSerializer.Deserialize<SendTextData>(send.Data);
                sendData = textData;
                Text = new SendTextModel(textData);
                break;
            default:
                throw new ArgumentException("Unsupported " + nameof(Type) + ".");
        }

        Name = sendData.Name;
        ExpirationDate = send.ExpirationDate;
    }

    public string Id { get; set; }
    public SendType Type { get; set; }
    public string Name { get; set; }
    public SendFileModel File { get; set; }
    public SendTextModel Text { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string CreatorIdentifier { get; set; }
}
