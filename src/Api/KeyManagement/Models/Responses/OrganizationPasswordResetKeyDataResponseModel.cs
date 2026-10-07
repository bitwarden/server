using System.Diagnostics.CodeAnalysis;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Responses;

public class OrganizationPasswordResetKeyDataResponseModel : ResponseModel
{
    private const string _objectName = "organizationPasswordResetKeyData";

    [SetsRequiredMembers]
    public OrganizationPasswordResetKeyDataResponseModel(OrganizationPasswordResetKeyData data,
        string obj = _objectName) : base(obj)
    {
        ArgumentNullException.ThrowIfNull(data);

        OrganizationId = data.OrganizationId;
        OrganizationName = data.OrganizationName;
        OrganizationPublicKey = data.OrganizationPublicKey;
    }

    public OrganizationPasswordResetKeyDataResponseModel() : base(_objectName)
    {
        OrganizationName = string.Empty;
    }

    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public required string OrganizationPublicKey { get; set; }
}
