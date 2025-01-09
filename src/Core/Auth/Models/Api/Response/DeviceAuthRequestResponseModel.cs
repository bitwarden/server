using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Core.Auth.Models.Api.Response;

public class DeviceAuthRequestResponseModel : ResponseModel
{
    public DeviceAuthRequestResponseModel()
        : base("device") { }

    public static DeviceAuthRequestResponseModel From(DeviceAuthDetails deviceAuthDetails)
    {
        var converted = new DeviceAuthRequestResponseModel
        {
            Id = deviceAuthDetails.Id,
            Name = deviceAuthDetails.Name,
            Type = deviceAuthDetails.Type,
            Identifier = deviceAuthDetails.Identifier,
            CreationDate = deviceAuthDetails.CreationDate,
            IsTrusted = deviceAuthDetails.IsTrusted()
        };

        if (deviceAuthDetails.AuthRequestId != null && deviceAuthDetails.AuthRequestCreatedAt != null)
        {
            converted.DevicePendingAuthRequest = new PendingAuthRequest
            {
                Id = (Guid)deviceAuthDetails.AuthRequestId,
                CreationDate = (DateTime)deviceAuthDetails.AuthRequestCreatedAt
            };
        }

        return converted;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public string Identifier { get; set; }
    public DateTime CreationDate { get; set; }
    public bool IsTrusted { get; set; }

    public PendingAuthRequest DevicePendingAuthRequest { get; set; }

    public class PendingAuthRequest
    {
        public Guid Id { get; set; }
        public DateTime CreationDate { get; set; }
    }
}
