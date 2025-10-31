// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System;
using Bit.Core.Models.Api;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;

namespace Bit.Api.Vault.Models.Response;

public class CipherHistoryResponseModel : ResponseModel
{
    public CipherHistoryResponseModel(CipherHistory history, string obj = "cipherHistory")
        : base(obj)
    {
        if (history == null)
        {
            throw new ArgumentNullException(nameof(history));
        }

        Id = history.Id;
        CipherId = history.CipherId;
        UserId = history.UserId;
        OrganizationId = history.OrganizationId;
        Type = history.Type;
        Data = history.Data;
        Favorites = history.Favorites;
        Folders = history.Folders;
        Attachments = history.Attachments;
        CreationDate = history.CreationDate;
        RevisionDate = history.RevisionDate;
        DeletedDate = history.DeletedDate;
        Reprompt = history.Reprompt;
        Key = history.Key;
        ArchivedDate = history.ArchivedDate;
        HistoryDate = history.HistoryDate;
    }

    public Guid Id { get; set; }
    public Guid CipherId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public CipherType Type { get; set; }
    public string Data { get; set; }
    public string Favorites { get; set; }
    public string Folders { get; set; }
    public string Attachments { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    public CipherRepromptType? Reprompt { get; set; }
    public string Key { get; set; }
    public DateTime? ArchivedDate { get; set; }
    public DateTime HistoryDate { get; set; }
}
