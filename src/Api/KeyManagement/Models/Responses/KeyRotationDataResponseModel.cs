using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Responses;

public class KeyRotationDataResponseModel : ResponseModel
{
    private const string _objectName = "keyRotationData";

    public KeyRotationDataResponseModel(KeyRotationData data, string obj = _objectName) : base(obj)
    {
        ArgumentNullException.ThrowIfNull(data);

        OrganizationPasswordResetKeyData =
            data.OrganizationPasswordResetKeyData.Select(o => new OrganizationPasswordResetKeyDataResponseModel(o));
        EmergencyAccessKeyData =
            data.EmergencyAccessKeyData.Select(ea => new EmergencyAccessKeyDataResponseModel(ea));
        TrustedDeviceKeyData =
            data.TrustedDeviceKeyData.Select(d => new TrustedDeviceKeyDataResponseModel(d));
        PasskeyKeyData =
            data.PasskeyKeyData.Select(p => new PasskeyKeyDataResponseModel(p));
    }

    public KeyRotationDataResponseModel() : base(_objectName)
    {
        OrganizationPasswordResetKeyData = [];
        EmergencyAccessKeyData = [];
        TrustedDeviceKeyData = [];
        PasskeyKeyData = [];
    }

    public IEnumerable<OrganizationPasswordResetKeyDataResponseModel> OrganizationPasswordResetKeyData { get; set; }
    public IEnumerable<EmergencyAccessKeyDataResponseModel> EmergencyAccessKeyData { get; set; }
    public IEnumerable<TrustedDeviceKeyDataResponseModel> TrustedDeviceKeyData { get; set; }
    public IEnumerable<PasskeyKeyDataResponseModel> PasskeyKeyData { get; set; }
}
