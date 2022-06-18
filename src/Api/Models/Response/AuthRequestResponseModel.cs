using System;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response
{
    public class AuthRequestResponseModel : ResponseModel
    {
        public AuthRequestResponseModel(AuthRequest authRequest, string obj = "auth-request")
            : base(obj)
        {
            if (authRequest == null)
            {
                throw new ArgumentNullException(nameof(authRequest));
            }

            Id = authRequest.Id.ToString();
            PublicKey = authRequest.PublicKey;
            RequestDeviceType = authRequest.RequestDeviceType;
            RequestIpAddress = authRequest.RequestIpAddress;
            Key = authRequest.Key;
            MasterPasswordHash = authRequest.MasterPasswordHash;
            CreationDate = authRequest.CreationDate;
        }

        public string Id { get; set; }
        public string PublicKey { get; set; }
        public DeviceType RequestDeviceType { get; set; }
        public string RequestIpAddress { get; set; }
        public string Key { get; set; }
        public string MasterPasswordHash { get; set; }
        public DateTime CreationDate { get; set; }
    }
}
