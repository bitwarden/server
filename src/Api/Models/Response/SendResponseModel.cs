using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response;

public class SendResponseModel : ResponseModel
{
    public SendResponseModel(Send send, GlobalSettings globalSettings)
        : base("send")
    {
        if (send == null)
        {
            throw new ArgumentNullException(nameof(send));
        }

        Id = send.Id.ToString();
        AccessId = CoreHelpers.Base64UrlEncode(send.Id.ToByteArray());
        Type = send.Type;
        Key = send.Key;
        MaxAccessCount = send.MaxAccessCount;
        AccessCount = send.AccessCount;
        RevisionDate = send.RevisionDate;
        ExpirationDate = send.ExpirationDate;
        DeletionDate = send.DeletionDate;
        Password = send.Password;
        Disabled = send.Disabled;
        HideEmail = send.HideEmail.GetValueOrDefault();

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
        Notes = sendData.Notes;
    }

    public string Id { get; set; }
    public string AccessId { get; set; }
    public SendType Type { get; set; }
    public string Name { get; set; }
    public string Notes { get; set; }
    public SendFileModel File { get; set; }
    public SendTextModel Text { get; set; }
    public string Key { get; set; }
    public int? MaxAccessCount { get; set; }
    public int AccessCount { get; set; }
    public string Password { get; set; }
    public bool Disabled { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime DeletionDate { get; set; }
    public bool HideEmail { get; set; }
}
