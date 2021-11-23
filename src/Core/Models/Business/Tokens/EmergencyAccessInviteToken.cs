using System;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Business.Tokens
{
    public class EmergencyAccessInviteToken : IProtectedData
    {
        public string Identifier { get; set; }
        public Guid Id { get; set; }
        public string Email { get; set; }
        public DateTime CreationTime { get; set; }

        public void Deserialize(string data)
        {
            var dataParts = data.Split(' ');

            if (dataParts.Length != 4)
            {
                return;
            }

            Identifier = dataParts[0];
            Id = new Guid(dataParts[1]);
            Email = dataParts[2];
            CreationTime = CoreHelpers.FromEpocMilliseconds(Convert.ToInt64(dataParts[3]));
        }

        public string Serialize()
        {
            var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
            return $"EmergencyAccessInvite {Id} {Email} {nowMillis}";
        }

        public bool IsValid(Guid id, string email, int expirationTimeInHours)
        {
            if (Identifier != "EmergencyAccessInvite")
            {
                return false;
            }

            if (Id != id)
            {
                return false;
            }

            if (!Email.Equals(email, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return CreationTime.AddHours(expirationTimeInHours) < DateTime.UtcNow;
        }
    }
}
