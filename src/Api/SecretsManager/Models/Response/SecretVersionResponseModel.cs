using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

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
    public string? EditorOrganizationUserName { get; set; }
    public string? EditorServiceAccountName { get; set; }

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

    public SecretVersionResponseModel(SecretVersionDetails details) : this(details.SecretVersion)
    {
        EditorOrganizationUserName = GetUserDisplayName(details.EditorUserName, details.EditorUserEmail);
        EditorServiceAccountName = details.EditorServiceAccountName;
    }

    private static string? GetUserDisplayName(string? name, string? email)
    {
        return string.IsNullOrWhiteSpace(name) ? email : name;
    }
}
