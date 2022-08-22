using Bit.Core.Utilities;

namespace Bit.Core.Entities
{
    public class AuthRequest : ITableObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Enums.AuthRequestType Type { get; set; }
        public string RequestDeviceIdentifier { get; set; }
        public Enums.DeviceType RequestDeviceType { get; set; }
        public string RequestIpAddress { get; set; }
        public string RequestFingerprint { get; set; }
        public Guid? ResponseDeviceId { get; set; }
        public string AccessCode { get; set; }
        public string PublicKey { get; set; }
        public string Key { get; set; }
        public string MasterPasswordHash { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime? ResponseDate { get; set; }
        public DateTime? AuthenticationDate { get; set; }

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }

        public bool isSpent()
        {
            return ResponseDate.HasValue || AuthenticationDate.HasValue || getExpirationDate() < DateTime.UtcNow;
        }

        public DateTime getExpirationDate()
        {
            return CreationDate.AddMinutes(15);
        }
    }
}
