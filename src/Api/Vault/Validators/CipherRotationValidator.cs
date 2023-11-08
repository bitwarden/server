using Bit.Api.Auth.Validators;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;

namespace Bit.Api.Vault.Validators;

public class CipherRotationValidator : IRotationValidator<IEnumerable<CipherWithIdRequestModel>, IEnumerable<Cipher>>
{
    private readonly ICipherRepository _cipherRepository;

    public CipherRotationValidator(ICipherRepository cipherRepository)
    {
        _cipherRepository = cipherRepository;
    }

    public async Task<IEnumerable<Cipher>> ValidateAsync(User user, IEnumerable<CipherWithIdRequestModel> ciphers)
    {
        if (!ciphers.Any())
        {
            return null;
        }

        var existingCiphers = await _cipherRepository.GetManyByUserIdAsync(user.Id);
        var result = new List<Cipher>();

        foreach (var existing in existingCiphers)
        {
            var cipher = ciphers.FirstOrDefault(c => c.Id == existing.Id);
            if (cipher == null)
            {
                throw new BadRequestException("All existing ciphers must be included in the rotation.");
            }

            result.Add(cipher.ToCipher(existing));
        }

        return result;
    }
}
