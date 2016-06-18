using System;
using Bit.Core.Domains;
using Bit.Core.Enums;

namespace Bit.Api.Models
{
    public class DeviceResponseModel : ResponseModel
    {
        public DeviceResponseModel(Device device)
            : base("device")
        {
            if(device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            Id = device.Id.ToString();
            Name = device.Name;
            Type = device.Type;
            CreationDate = device.CreationDate;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public DeviceType Type { get; set; }
        public DateTime CreationDate { get; set; }
    }
}
