using Bit.Api.Models.Response;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [SecretsManager]
    public class SecretsController : Controller
    {
        private readonly ISecretRepository _secretRepository;
        private readonly ICreateSecretCommand _createSecretCommand;
        private readonly IUpdateSecretCommand _updateSecretCommand;

        public SecretsController(ISecretRepository secretRepository, ICreateSecretCommand createSecretCommand, IUpdateSecretCommand updateSecretCommand)
        {
            _secretRepository = secretRepository;
            _createSecretCommand = createSecretCommand;
            _updateSecretCommand = updateSecretCommand;
        }

        [HttpGet("organizations/{organizationId}/secrets")]
        public async Task<ListResponseModel<SecretIdentifierResponseModel>> GetSecretsByOrganizationAsync([FromRoute] Guid organizationId)
        {
            var secrets = await _secretRepository.GetManyByOrganizationIdAsync(organizationId);
            if (secrets == null || !secrets.Any())
            {
                throw new NotFoundException();
            }
            var responses = secrets.Select(secret => new SecretIdentifierResponseModel(secret));
            return new ListResponseModel<SecretIdentifierResponseModel>(responses);
        }


        [HttpGet("secrets/{id}")]
        public async Task<SecretResponseModel> GetSecretAsync([FromRoute] Guid id)
        {
            var secret = await _secretRepository.GetByIdAsync(id);
            if (secret == null)
            {
                throw new NotFoundException();
            }
            return new SecretResponseModel(secret);
        }

        [HttpPost("organizations/{organizationId}/secrets")]
        public async Task<SecretResponseModel> CreateSecretAsync([FromRoute] Guid organizationId, [FromBody] SecretCreateRequestModel createRequest)
        {
            if (organizationId != createRequest.OrganizationId)
            {
                throw new BadRequestException("Organization ID does not match.");
            }

            var result = await _createSecretCommand.CreateAsync(createRequest.ToSecret());
            return new SecretResponseModel(result);
        }

        [HttpPut("secrets/{id}")]
        public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
        {
            var result = await _updateSecretCommand.UpdateAsync(updateRequest.ToSecret(id));
            return new SecretResponseModel(result);
        }
    }
}
