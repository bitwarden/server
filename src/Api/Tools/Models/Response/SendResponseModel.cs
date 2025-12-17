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
/// A response issued to a Bitwarden client in response to ownership operations.
/// </summary>
/// <seealso cref="SendAccessResponseModel" />
public class SendResponseModel : ResponseModel
{
    /// <summary>
    /// Instantiates a send response model
    /// </summary>
    /// <param name="send">Content to transmit to the client.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="send"/> is <see langword="null" />
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="send" /> has an invalid <see cref="Send.Type"/>.
    /// </exception>
    public SendResponseModel(Send send)
        : base("send")
    {
        if (send == null)
        {
            throw new ArgumentNullException(nameof(send));
        }

        Id = send.Id;
        AccessId = CoreHelpers.Base64UrlEncode(send.Id.ToByteArray());
        Type = send.Type;
        AuthType = send.AuthType ?? (!string.IsNullOrWhiteSpace(send.Password)
            ? AuthType = Core.Tools.Enums.AuthType.Password
            : (!string.IsNullOrWhiteSpace(send.Emails)? Core.Tools.Enums.AuthType.Email : Core.Tools.Enums.AuthType.None));
        Key = send.Key;
        MaxAccessCount = send.MaxAccessCount;
        AccessCount = send.AccessCount;
        RevisionDate = send.RevisionDate;
        ExpirationDate = send.ExpirationDate;
        DeletionDate = send.DeletionDate;
        Password = send.Password;
        Emails = send.Emails;
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

    /// <summary>
    /// Identifies the send to its owner
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifies the send in a send URL
    /// </summary>
    public string AccessId { get; set; }

    /// <summary>
    /// Indicates whether the send contains text or file data.
    /// </summary>
    public SendType Type { get; set; }

    /// <summary>
    /// Specifies the authentication method required to access this Send.
    /// </summary>
    public AuthType? AuthType { get; set; }

    /// <summary>
    /// Label for the send.
    /// </summary>
    /// <remarks>
    /// This field contains a base64-encoded byte array. The array contains
    /// the E2E-encrypted encrypted content.
    /// </remarks>
    public string Name { get; set; }

    /// <summary>
    /// Notes for the send. This is only visible to the owner of the send.
    /// This field is encrypted.
    /// </summary>
    /// <remarks>
    /// This field contains a base64-encoded byte array. The array contains
    /// the E2E-encrypted  encrypted content.
    /// </remarks>
    public string Notes { get; set; }

    /// <summary>
    /// Contains file metadata uploaded with the send.
    /// The file content is uploaded separately.
    /// </summary>
    public SendFileModel File { get; set; }

    /// <summary>
    /// Contains text data uploaded with the send.
    /// </summary>
    public SendTextModel Text { get; set; }

    /// <summary>
    /// A base64-encoded byte array containing the Send's encryption key.
    /// It's also provided to send recipients in the Send's URL.
    /// </summary>
    /// <remarks>
    /// This field contains a base64-encoded byte array. The array contains
    /// the E2E-encrypted content.
    /// </remarks>
    public string Key { get; set; }

    /// <summary>
    /// The maximum number of times a send can be accessed before it expires.
    /// When this value is <see langword="null" />, there is no limit.
    /// </summary>
    public int? MaxAccessCount { get; set; }

    /// <summary>
    /// The number of times a send has been accessed since it was created.
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Base64-encoded byte array of a password hash that grants access to the send.
    /// Mutually exclusive with <see cref="Emails"/>.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Comma-separated list of emails that may access the send using OTP
    /// authentication. Mutually exclusive with <see cref="Password"/>.
    /// </summary>
    public string Emails { get; set; }

    /// <summary>
    /// When <see langword="true"/>, send access is disabled.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// The last time this send's data changed.
    /// </summary>
    public DateTime RevisionDate { get; set; }

    /// <summary>
    /// The date after which a send cannot be accessed. When this value is
    /// <see langword="null"/>, there is no expiration date.
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// The date after which a send may be automatically deleted from the server.
    /// </summary>
    public DateTime DeletionDate { get; set; }

    /// <summary>
    /// When <see langword="true"/> send access hides the user's email address
    /// and displays a confirmation message instead.
    /// </summary>
    public bool HideEmail { get; set; }
}
