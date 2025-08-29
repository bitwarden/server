// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

using static System.StringSplitOptions;

namespace Bit.Api.Tools.Models.Request;

/// <summary>
/// A send request issued by a Bitwarden client
/// </summary>
public class SendRequestModel
{
    /// <summary>
    /// Indicates whether the send contains text or file data.
    /// </summary>
    public SendType Type { get; set; }

    /// <summary>
    /// Estimated length of the file accompanying the send. <see langword="null"/> when
    /// <see cref="Type"/> is <see cref="SendType.Text"/>.
    /// </summary>
    public long? FileLength { get; set; } = null;

    /// <summary>
    /// Label for the send.
    /// </summary>
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Name { get; set; }

    /// <summary>
    /// Notes for the send. This is only visible to the owner of the send.
    /// </summary>
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Notes { get; set; }

    /// <summary>
    /// A base64-encoded byte array containing the Send's encryption key. This key is
    /// also provided to send recipients in the Send's URL.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Key { get; set; }

    /// <summary>
    /// The maximum number of times a send can be accessed before it expires.
    /// When this value is <see langword="null" />, there is no limit.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxAccessCount { get; set; }

    /// <summary>
    /// The date after which a send cannot be accessed. When this value is
    /// <see langword="null"/>, there is no expiration date.
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// The date after which a send may be automatically deleted from the server.
    /// When this is <see langword="null" />, the send may be deleted after it has
    /// exceeded the global send timeout limit.
    /// </summary>
    [Required]
    public DateTime? DeletionDate { get; set; }

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
    /// Base64-encoded byte array of a password hash that grants access to the send.
    /// Mutually exclusive with <see cref="Emails"/>.
    /// </summary>
    [StringLength(1000)]
    public string Password { get; set; }

    /// <summary>
    /// Comma-separated list of emails that may access the send using OTP
    /// authentication. Mutually exclusive with <see cref="Password"/>.
    /// </summary>
    [StringLength(1024)]
    public string Emails { get; set; }

    /// <summary>
    /// When <see langword="true"/>, send access is disabled.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    [Required]
    public bool? Disabled { get; set; }

    /// <summary>
    /// When <see langword="true"/> send access hides the user's email address
    /// and displays a confirmation message instead. Defaults to <see langword="false"/>.
    /// </summary>
    public bool? HideEmail { get; set; }

    /// <summary>
    /// Transforms the request into a send object.
    /// </summary>
    /// <param name="userId">The user that owns the send.</param>
    /// <param name="sendAuthorizationService">Hashes the send password.</param>
    /// <returns>The send object</returns>
    public Send ToSend(Guid userId, ISendAuthorizationService sendAuthorizationService)
    {
        var send = new Send
        {
            Type = Type,
            UserId = (Guid?)userId
        };
        ToSend(send, sendAuthorizationService);
        return send;
    }

    /// <summary>
    /// Transforms the request into a send object and file data.
    /// </summary>
    /// <param name="userId">The user that owns the send.</param>
    /// <param name="fileName">Name of the file uploaded with the send.</param>
    /// <param name="sendAuthorizationService">Hashes the send password.</param>
    /// <returns>The send object and file data.</returns>
    public (Send, SendFileData) ToSend(Guid userId, string fileName, ISendAuthorizationService sendAuthorizationService)
    {
        // FIXME: This method does two things: creates a send and a send file data.
        //        It should only do one thing.
        var send = ToSendBase(new Send
        {
            Type = Type,
            UserId = (Guid?)userId
        }, sendAuthorizationService);
        var data = new SendFileData(Name, Notes, fileName);
        return (send, data);
    }

    /// <summary>
    /// Update a send object with request content
    /// </summary>
    /// <param name="existingSend">The send to update</param>
    /// <param name="sendAuthorizationService">Hashes the send password.</param>
    /// <returns>The send object</returns>
    // FIXME: rename to `UpdateSend`
    public Send ToSend(Send existingSend, ISendAuthorizationService sendAuthorizationService)
    {
        existingSend = ToSendBase(existingSend, sendAuthorizationService);
        switch (existingSend.Type)
        {
            case SendType.File:
                var fileData = JsonSerializer.Deserialize<SendFileData>(existingSend.Data);
                fileData.Name = Name;
                fileData.Notes = Notes;
                existingSend.Data = JsonSerializer.Serialize(fileData, JsonHelpers.IgnoreWritingNull);
                break;
            case SendType.Text:
                existingSend.Data = JsonSerializer.Serialize(ToSendTextData(), JsonHelpers.IgnoreWritingNull);
                break;
            default:
                throw new ArgumentException("Unsupported type: " + nameof(Type) + ".");
        }
        return existingSend;
    }

    /// <summary>
    /// Validates that the request is internally consistent for send creation.
    /// </summary>
    /// <exception cref="BadRequestException">
    /// Thrown when the send's expiration date has already expired.
    /// </exception>
    public void ValidateCreation()
    {
        var now = DateTime.UtcNow;
        // Add 1 minute for a sane buffer and client clock float
        var nowPlus1Minute = now.AddMinutes(1);
        if (ExpirationDate.HasValue && ExpirationDate.Value <= nowPlus1Minute)
        {
            throw new BadRequestException("You cannot create a Send that is already expired. " +
                "Adjust the expiration date and try again.");
        }
        ValidateEdit();
    }

    /// <summary>
    /// Validates that the request is internally consistent for send administration.
    /// </summary>
    /// <exception cref="BadRequestException">
    /// Thrown when the send's deletion date has already expired or when its
    /// expiration occurs after its deletion.
    /// </exception>
    public void ValidateEdit()
    {
        var now = DateTime.UtcNow;
        // Add 1 minute for a sane buffer and client clock float
        var nowPlus1Minute = now.AddMinutes(1);
        if (DeletionDate.HasValue)
        {
            if (DeletionDate.Value <= nowPlus1Minute)
            {
                throw new BadRequestException("You cannot have a Send with a deletion date in the past. " +
                    "Adjust the deletion date and try again.");
            }
            if (DeletionDate.Value > now.AddDays(31))
            {
                throw new BadRequestException("You cannot have a Send with a deletion date that far " +
                    "into the future. Adjust the Deletion Date to a value less than 31 days from now " +
                    "and try again.");
            }
        }
        if (ExpirationDate.HasValue)
        {
            if (ExpirationDate.Value <= nowPlus1Minute)
            {
                throw new BadRequestException("You cannot have a Send with an expiration date in the past. " +
                    "Adjust the expiration date and try again.");
            }
            if (ExpirationDate.Value > DeletionDate.Value)
            {
                throw new BadRequestException("You cannot have a Send with an expiration date greater than the deletion date. " +
                    "Adjust the expiration date and try again.");
            }
        }
    }

    private Send ToSendBase(Send existingSend, ISendAuthorizationService authorizationService)
    {
        existingSend.Key = Key;
        existingSend.ExpirationDate = ExpirationDate;
        existingSend.DeletionDate = DeletionDate.Value;
        existingSend.MaxAccessCount = MaxAccessCount;

        if (!string.IsNullOrWhiteSpace(Emails))
        {
            // normalize encoding
            var emails = Emails.Split(',', RemoveEmptyEntries | TrimEntries);
            existingSend.Emails = string.Join(", ", emails);
            existingSend.Password = null;
        }
        else if (!string.IsNullOrWhiteSpace(Password))
        {
            existingSend.Password = authorizationService.HashPassword(Password);
            existingSend.Emails = null;
        }

        existingSend.Disabled = Disabled.GetValueOrDefault();
        existingSend.HideEmail = HideEmail.GetValueOrDefault();

        return existingSend;
    }

    private SendTextData ToSendTextData()
    {
        return new SendTextData(Name, Notes, Text.Text, Text.Hidden);
    }
}

/// <summary>
/// A send request issued by a Bitwarden client
/// </summary>
public class SendWithIdRequestModel : SendRequestModel
{
    /// <summary>
    /// Identifies the send. When this is <see langword="null" />, the client is requesting
    /// a new send.
    /// </summary>
    [Required]
    public Guid? Id { get; set; }
}
