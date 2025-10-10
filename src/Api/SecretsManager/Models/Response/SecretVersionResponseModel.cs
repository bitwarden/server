#nullable enable

using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretVersionResponseModel : ResponseModel
{
    private const string _objectName = "secretVersion";

    public Guid Id { get; set; }
    public Guid SecretId { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime VersionDate { get; set; }
    public Guid? EditorServiceAccountId { get; set; }
    public Guid? EditorOrganizationUserId { get; set; }

    public SecretVersionResponseModel() : base(_objectName) { }

    public SecretVersionResponseModel(SecretVersion secretVersion) : base(_objectName)
    {
        Id = secretVersion.Id;
        SecretId = secretVersion.SecretId;
        Value = secretVersion.Value;
        VersionDate = secretVersion.VersionDate;
        EditorServiceAccountId = secretVersion.EditorServiceAccountId;
        EditorOrganizationUserId = secretVersion.EditorOrganizationUserId;
    }
}
