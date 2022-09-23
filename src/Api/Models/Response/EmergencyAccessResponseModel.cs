using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Core.Models.Data;

namespace Bit.Api.Models.Response;

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
    public EmergencyAccessTakeoverResponseModel(EmergencyAccess emergencyAccess, User grantor, string obj = "emergencyAccessTakeover") : base(obj)
    {
        if (emergencyAccess == null)
        {
            throw new ArgumentNullException(nameof(emergencyAccess));
        }

        KeyEncrypted = emergencyAccess.KeyEncrypted;
        Kdf = grantor.Kdf;
        KdfIterations = grantor.KdfIterations;
    }

    public int KdfIterations { get; private set; }
    public KdfType Kdf { get; private set; }
    public string KeyEncrypted { get; private set; }
}

public class EmergencyAccessViewResponseModel : ResponseModel
{
    public EmergencyAccessViewResponseModel(
        IGlobalSettings globalSettings,
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
