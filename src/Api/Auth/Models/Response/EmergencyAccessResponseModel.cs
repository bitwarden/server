// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Vault.Models.Response;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Settings;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Auth.Models.Response;

public class EmergencyAccessResponseModel : ResponseModel
{
    public EmergencyAccessResponseModel(EmergencyAccess emergencyAccess, string obj = "emergencyAccess") : base(obj)
    {
        if (emergencyAccess == null)
        {
            throw new ArgumentNullException(nameof(emergencyAccess));
        }

        Id = emergencyAccess.Id;
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

        Id = emergencyAccess.Id;
        Status = emergencyAccess.Status;
        Type = emergencyAccess.Type;
        WaitTimeDays = emergencyAccess.WaitTimeDays;
    }

    public Guid Id { get; private set; }
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

        GranteeId = emergencyAccess.GranteeId;
        Email = emergencyAccess.GranteeEmail;
        Name = emergencyAccess.GranteeName;
        AvatarColor = emergencyAccess.GranteeAvatarColor;
    }

    public Guid? GranteeId { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string AvatarColor { get; private set; }
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

        GrantorId = emergencyAccess.GrantorId;
        Email = emergencyAccess.GrantorEmail;
        Name = emergencyAccess.GrantorName;
        AvatarColor = emergencyAccess.GrantorAvatarColor;
    }

    public Guid GrantorId { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string AvatarColor { get; private set; }
}

public class EmergencyAccessTakeoverResponseModel : ResponseModel
{
    /// <summary>
    /// Creates a new instance of the <see cref="EmergencyAccessTakeoverResponseModel"/> class.
    /// </summary>
    /// <param name="emergencyAccess">Consumed for the Encrypted Key value</param>
    /// <param name="grantor">consumed for the KDF configuration</param>
    /// <param name="obj">name of the object</param>
    /// <exception cref="ArgumentNullException">emergencyAccess cannot be null</exception>
    public EmergencyAccessTakeoverResponseModel(EmergencyAccess emergencyAccess, User grantor, string obj = "emergencyAccessTakeover") : base(obj)
    {
        if (emergencyAccess == null)
        {
            throw new ArgumentNullException(nameof(emergencyAccess));
        }

        KeyEncrypted = emergencyAccess.KeyEncrypted;
        Kdf = grantor.Kdf;
        KdfIterations = grantor.KdfIterations;
        KdfMemory = grantor.KdfMemory;
        KdfParallelism = grantor.KdfParallelism;
    }

    public int KdfIterations { get; private set; }
    public int? KdfMemory { get; private set; }
    public int? KdfParallelism { get; private set; }
    public KdfType Kdf { get; private set; }
    public string KeyEncrypted { get; private set; }
}

public class EmergencyAccessViewResponseModel : ResponseModel
{
    public EmergencyAccessViewResponseModel(
        IGlobalSettings globalSettings,
        EmergencyAccess emergencyAccess,
        IEnumerable<CipherDetails> ciphers,
        User user)
        : base("emergencyAccessView")
    {
        KeyEncrypted = emergencyAccess.KeyEncrypted;
        Ciphers = ciphers.Select(cipher =>
            new CipherResponseModel(
                cipher,
                user,
                organizationAbilities: null, // Emergency access only retrieves personal ciphers so organizationAbilities is not needed
                globalSettings));
    }

    public string KeyEncrypted { get; set; }
    public IEnumerable<CipherResponseModel> Ciphers { get; set; }
}
