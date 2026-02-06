#nullable enable
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

public interface IRegenerateUserAsymmetricKeysCommand
{
    Task RegenerateKeysAsync(UserAsymmetricKeys userAsymmetricKeys,
        ICollection<OrganizationUser> usersOrganizationAccounts,
        ICollection<EmergencyAccessDetails> designatedEmergencyAccess);
}
