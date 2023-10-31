
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth;
using Bit.Core.Entities;

public class AccountRecoveryRotationValidator : IRotationValidator<IEnumerable<AccountRecoveryWithIdRequestModel>, IEnumerable<OrganizationUser>>
{
    public Task<IEnumerable<OrganizationUser>> ValidateAsync(User user, IEnumerable<AccountRecoveryWithIdRequestModel> data) => throw new NotImplementedException();
}
