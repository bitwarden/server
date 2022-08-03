using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [SecretsManager]
    public class SecretsController : Controller
    {
        private readonly ISecretRepository _secretRepository;

        public SecretsController(ISecretRepository secretRepository)
        {
            _secretRepository = secretRepository;
        }

        [HttpGet("organizations/{orgId}/secrets")]
        public async Task<ListResponseModel<SecretResponseModel>> Get([FromRoute]Guid orgId)
        {
            var results = await _secretRepository.GetManyByOrganizationIdAsync(orgId);
            var responses = results.Select(secret => new SecretResponseModel(secret));
            return new ListResponseModel<SecretResponseModel>(responses);
        }
    }
}
