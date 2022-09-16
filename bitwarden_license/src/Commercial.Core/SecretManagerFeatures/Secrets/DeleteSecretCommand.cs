using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets
{
    public class DeleteSecretCommand : IDeleteSecretCommand
    {
        private readonly ISecretRepository _secretRepository;

        public DeleteSecretCommand(ISecretRepository secretRepository)
        {
            _secretRepository = secretRepository;
        }

        public async Task<List<Tuple<Guid, string>>> DeleteSecrets(List<Guid> ids)
        {
            var secrets = await _secretRepository.GetManyByIds(ids);

            if (secrets?.Any() != true)
            {
                throw new NotFoundException();
            }

            var results = ids.Select(id =>
            {
                if (!secrets.Any(secret => secret.Id == id))
                {
                    throw new NotFoundException();
                }
                // TODO Once permissions are implemented add check for each secret here.
                else
                {
                    return new Tuple<Guid, string>(id, "");
                }
            }).ToList();

            await _secretRepository.SoftDeleteManyByIdAsync(ids);
            return results;
        }
    }
}

