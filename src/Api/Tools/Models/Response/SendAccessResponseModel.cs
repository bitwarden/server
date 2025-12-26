// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.Models.Api;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Tools.Models.Response;

/// <summary>
/// A response issued to a Bitwarden client in response to access operations.
/// </summary>
public class SendAccessResponseModel : ResponseModel
{
    /// <summary>
    /// Instantiates a send access response model
    /// </summary>
    /// <param name="send">Content to transmit to the client.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="send"/> is <see langword="null" />
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="send" /> has an invalid <see cref="Send.Type"/>.
    /// </exception>
    public SendAccessResponseModel(Send send)
        : base("send-access")
    {
        if (send == null)
        {
            throw new ArgumentNullException(nameof(send));
        }

        Id = CoreHelpers.Base64UrlEncode(send.Id.ToByteArray());
        Type = send.Type;
        AuthType = send.AuthType;

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

    /// <summary>
    /// Identifies the send in a send URL
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Indicates whether the send contains text or file data.
    /// </summary>
    public SendType Type { get; set; }

    /// <summary>
    /// Specifies the authentication method required to access this Send.
    /// </summary>
    public AuthType? AuthType { get; set; }

    /// <summary>
    /// Label for the send. This is only visible to the owner of the send.
    /// </summary>
    /// <remarks>
    /// This field contains a base64-encoded byte array. The array contains
    /// the E2E-encrypted encrypted content.
    /// </remarks>
    public string Name { get; set; }

    /// <summary>
    /// Describes the file attached to the send.
    /// </summary>
    /// <remarks>
    /// File content is downloaded separately using
    /// <see cref="Bit.Api.Tools.Controllers.SendsController.GetSendFileDownloadData" />
    /// </remarks>
    public SendFileModel File { get; set; }

    /// <summary>
    /// Contains text data uploaded with the send.
    /// </summary>
    public SendTextModel Text { get; set; }

    /// <summary>
    /// The date after which a send cannot be accessed. When this value is
    /// <see langword="null"/>, there is no expiration date.
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Indicates the person that created the send to the accessor.
    /// </summary>
    public string CreatorIdentifier { get; set; }
}
