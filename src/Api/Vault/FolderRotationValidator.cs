
using Bit.Api.Auth;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Vault.Entities;

public class FolderRotationValidator : IRotationValidator<IEnumerable<FolderWithIdRequestModel>, IEnumerable<Folder>>
{
    public async Task<IEnumerable<Folder>> ValidateAsync(Guid userId, IEnumerable<FolderWithIdRequestModel> data) => throw new NotImplementedException();
}
