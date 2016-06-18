using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Domains;
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
        [StringLength(255)]
        public string PushToken { get; set; }

        public Device ToDevice(string userId = null)
        {
            return ToDevice(new Device
            {
                UserId = new Guid(userId)
            });
        }

        public Device ToDevice(Device existingDevice)
        {
            existingDevice.Name = Name;
            existingDevice.PushToken = PushToken;
            existingDevice.Type = Type.Value;

            return existingDevice;
        }
    }
}
