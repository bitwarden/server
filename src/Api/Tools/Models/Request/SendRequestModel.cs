﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Api.Tools.Models.Request;

public class SendRequestModel
{
    public SendType Type { get; set; }
    public long? FileLength { get; set; } = null;
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Name { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Notes { get; set; }
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Key { get; set; }
    [Range(1, int.MaxValue)]
    public int? MaxAccessCount { get; set; }
    public DateTime? ExpirationDate { get; set; }
    [Required]
    public DateTime? DeletionDate { get; set; }
    public SendFileModel File { get; set; }
    public SendTextModel Text { get; set; }
    [StringLength(1000)]
    public string Password { get; set; }
    [Required]
    public bool? Disabled { get; set; }
    public bool? HideEmail { get; set; }

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

    public (Send, SendFileData) ToSend(Guid userId, string fileName, ISendAuthorizationService sendAuthorizationService)
    {
        var send = ToSendBase(new Send
        {
            Type = Type,
            UserId = (Guid?)userId
        }, sendAuthorizationService);
        var data = new SendFileData(Name, Notes, fileName);
        return (send, data);
    }

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
        if (!string.IsNullOrWhiteSpace(Password))
        {
            existingSend.Password = authorizationService.HashPassword(Password);
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

public class SendWithIdRequestModel : SendRequestModel
{
    [Required]
    public Guid? Id { get; set; }
}
