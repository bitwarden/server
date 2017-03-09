using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class DeviceRequestModel
    {
        [Required]
        public DeviceType? Type { get; set; }
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        [Required]
        [StringLength(50)]
        public string Identifier { get; set; }
        [StringLength(255)]
        public string PushToken { get; set; }

        public Device ToDevice(Guid? userId = null)
        {
            return ToDevice(new Device
            {
                UserId = userId == null ? default(Guid) : userId.Value
            });
        }

        public Device ToDevice(Device existingDevice)
        {
            existingDevice.Name = Name;
            existingDevice.Identifier = Identifier;
            existingDevice.PushToken = PushToken;
            existingDevice.Type = Type.Value;

            return existingDevice;
        }
    }

    public class DeviceTokenRequestModel
    {
        [StringLength(255)]
        public string PushToken { get; set; }

        public Device ToDevice(Device existingDevice)
        {
            existingDevice.PushToken = PushToken;
            return existingDevice;
        }
    }
}
