using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Core.Models.Data;

namespace Bit.Core.Models.Api.Response
{
    public class EmergencyAccessResponseModel : ResponseModel
    {
        public EmergencyAccessResponseModel(EmergencyAccess emergencyAccess, string obj = "emergencyAccess") : base(obj)
        {
            if (emergencyAccess == null)
            {
                throw new ArgumentNullException(nameof(emergencyAccess));
            }

            Id = emergencyAccess.Id.ToString();
            Status = emergencyAccess.Status;
            Type = emergencyAccess.Type;
            WaitTimeDays = emergencyAccess.WaitTimeDays;
        }

        public EmergencyAccessResponseModel(EmergencyAccessDetails emergencyAccess, string obj = "emergencyAccess") : base(obj)
        {
            if (emergencyAccess == null)
            {
                throw new ArgumentNullException(nameof(emergencyAccess));
            }

            Id = emergencyAccess.Id.ToString();
            Status = emergencyAccess.Status;
            Type = emergencyAccess.Type;
            WaitTimeDays = emergencyAccess.WaitTimeDays;
        }

        public string Id { get; private set; }
        public EmergencyAccessStatusType Status { get; private set; }
        public EmergencyAccessType Type { get; private set; }
        public int WaitTimeDays { get; private set; }
    }

    public class EmergencyAccessGranteeDetailsResponseModel : EmergencyAccessResponseModel
    {
        public EmergencyAccessGranteeDetailsResponseModel(EmergencyAccessDetails emergencyAccess)
            : base(emergencyAccess, "emergencyAccessGranteeDetails")
        {
            if (emergencyAccess == null)
            {
                throw new ArgumentNullException(nameof(emergencyAccess));
            }

            GranteeId = emergencyAccess.GranteeId.ToString();
            Email = emergencyAccess.GranteeEmail;
            Name = emergencyAccess.GranteeName;
        }

        public string GranteeId { get; private set; }
        public string Name { get; private set; }
        public string Email { get; private set; }
    }

    public class EmergencyAccessGrantorDetailsResponseModel : EmergencyAccessResponseModel
    {
        public EmergencyAccessGrantorDetailsResponseModel(EmergencyAccessDetails emergencyAccess)
            : base(emergencyAccess, "emergencyAccessGrantorDetails")
        {
            if (emergencyAccess == null)
            {
                throw new ArgumentNullException(nameof(emergencyAccess));
            }

            GrantorId = emergencyAccess.GrantorId.ToString();
            Email = emergencyAccess.GrantorEmail;
            Name = emergencyAccess.GrantorName;
        }

        public string GrantorId { get; private set; }
        public string Name { get; private set; }
        public string Email { get; private set; }
    }

    public class EmergencyAccessTakeoverResponseModel : ResponseModel
    {
        public EmergencyAccessTakeoverResponseModel(EmergencyAccess emergencyAccess, User grantor, ICollection<Policy> policy, string obj = "emergencyAccessTakeover") : base(obj)
        {
            if (emergencyAccess == null)
            {
                throw new ArgumentNullException(nameof(emergencyAccess));
            }

            KeyEncrypted = emergencyAccess.KeyEncrypted;
            Kdf = grantor.Kdf;
            KdfIterations = grantor.KdfIterations;
            Policy = policy?.Select<Policy, PolicyResponseModel>(policy => new PolicyResponseModel(policy));
        }

        public int KdfIterations { get; private set; }
        public KdfType Kdf { get; private set; }
        public string KeyEncrypted { get; private set; }
        public IEnumerable<PolicyResponseModel> Policy { get; private set; }
    }

    public class EmergencyAccessViewResponseModel : ResponseModel
    {
        public EmergencyAccessViewResponseModel(
            GlobalSettings globalSettings,
            EmergencyAccess emergencyAccess,
            IEnumerable<CipherDetails> ciphers)
            : base("emergencyAccessView")
        {
            KeyEncrypted = emergencyAccess.KeyEncrypted;
            Ciphers = ciphers.Select(c => new CipherResponseModel(c, globalSettings));
        }
        
        public string KeyEncrypted { get; set; }
        public IEnumerable<CipherResponseModel> Ciphers { get; set; }
    }
}
