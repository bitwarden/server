using System.Diagnostics.CodeAnalysis;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Responses;

public class EmergencyAccessKeyDataResponseModel : ResponseModel
{
    private const string _objectName = "emergencyAccessKeyData";

    [SetsRequiredMembers]
    public EmergencyAccessKeyDataResponseModel(EmergencyAccessKeyData data, string obj = _objectName) : base(obj)
    {
        ArgumentNullException.ThrowIfNull(data);

        Id = data.Id;
        GranteeId = data.GranteeId;
        GranteeName = data.GranteeName;
        GranteeEmail = data.GranteeEmail;
        PublicKey = data.PublicKey;
    }

    public EmergencyAccessKeyDataResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }
    public Guid? GranteeId { get; set; }
    public string? GranteeName { get; set; }
    public string? GranteeEmail { get; set; }
    public required string PublicKey { get; set; }
}
